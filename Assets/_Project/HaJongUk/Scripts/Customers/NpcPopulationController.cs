using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Result = OpenSourceTeam6.Customers.Unity.NpcStepResult;

namespace OpenSourceTeam6.Customers.Unity
{
    public readonly struct CheckoutWaitSnapshot
    {
        public readonly int CustomerId;
        public readonly int Position;
        public readonly float ElapsedSeconds;
        public readonly bool IsCheckingOut;

        public CheckoutWaitSnapshot(int customerId, int position, float elapsedSeconds, bool isCheckingOut)
        {
            CustomerId = customerId;
            Position = position;
            ElapsedSeconds = elapsedSeconds;
            IsCheckingOut = isCheckingOut;
        }
    }

    [DisallowMultipleComponent]
    public sealed class NpcPopulationController : MonoBehaviour
    {
        [Serializable]
        private struct DayPopulation
        {
            [Min(1)] public int day;
            [Min(0)] public int customers;
            [Min(0)] public int pedestrians;

            public DayPopulation(int day, int customers, int pedestrians)
            {
                this.day = day;
                this.customers = customers;
                this.pedestrians = pedestrians;
            }
        }

        private enum Role { Customer, Pedestrian }
        private enum Step { Enter, Browse, Queue, Checkout, Exit, Outside, WalkSidewalk }

        private static readonly Step[] CustomerSequence =
        {
            Step.Enter, Step.Browse, Step.Queue, Step.Checkout, Step.Exit, Step.Outside
        };

        private static readonly Step[] PedestrianSequence = { Step.WalkSidewalk };

        private sealed class AgentRun
        {
            public readonly NavMeshAgent Agent;
            public readonly Role Role;
            public readonly int Id;
            public readonly Step[] Sequence;
            public readonly Vector3 SidewalkDestination;
            public readonly NpcNavigation Navigation = new NpcNavigation();
            public int StepIndex;
            public float BrowseSeconds;
            public GameObject CarriedItem;

            public AgentRun(NavMeshAgent agent, Role role, int id, Vector3 sidewalkDestination)
            {
                Agent = agent;
                Role = role;
                Id = id;
                SidewalkDestination = sidewalkDestination;
                Sequence = role == Role.Customer ? CustomerSequence : PedestrianSequence;
            }
        }

        [Header("일차별 인원")]
        [SerializeField] private DayPopulation[] dayPopulations =
        {
            new DayPopulation(1, 2, 2),
            new DayPopulation(2, 3, 4),
            new DayPopulation(3, 4, 6)
        };

        [SerializeField, Range(1, 8)] private int maxPopulation = 8;
        [Header("테스트 프리팹")]
        [SerializeField] private GameObject customerPrefab;
        [SerializeField] private GameObject pedestrianPrefab;
        [SerializeField] private GameObject productPrefab;
        [Header("공용 보도 출입 구역")]
        [SerializeField] private Transform leftSidewalkZone;
        [SerializeField] private Transform rightSidewalkZone;
        [SerializeField] private Vector2 sidewalkZoneSize = new Vector2(4f, 4.8f);
        [Header("손님 동선")]
        [SerializeField] private Transform entrance;
        [SerializeField] private Transform shelf;
        [SerializeField] private Transform checkout;
        [SerializeField] private Transform[] queueSlots = Array.Empty<Transform>();
        [SerializeField] private Transform exit;
        [Header("테스트 시간")]
        [SerializeField, Min(0.25f)] private float spawnInterval = 1f;
        [SerializeField, Min(0)] private float browseDuration = 0.6f;
        [SerializeField, Min(0.1f)] private float checkoutDuration = 3f;

        private readonly List<AgentRun> customers = new List<AgentRun>(8);
        private readonly List<AgentRun> pedestrians = new List<AgentRun>(8);
        private readonly NpcCheckoutQueue checkoutQueue = new NpcCheckoutQueue();
        private float nextCustomerSpawn;
        private float nextPedestrianSpawn;
        private int nextId;

        public int Day { get; private set; } = 1;
        public int CustomerTarget { get; private set; }
        public int PedestrianTarget { get; private set; }
        public int ActiveCustomers => customers.Count;
        public int ActivePedestrians => pedestrians.Count;
        public int MaxPopulation => Mathf.Clamp(maxPopulation, 1, 8);
        public int MaxCustomers => Mathf.Min(MaxPopulation, queueSlots.Length);
        public int CompletedCustomers { get; private set; }
        public int CompletedPedestrians { get; private set; }
        public int SuccessfulMockCheckouts { get; private set; }
        public int RouteFailures { get; private set; }
        public bool IsPaused { get; private set; }

        public void SetDay(int day)
        {
            Day = Mathf.Max(1, day);
            DayPopulation selected = new DayPopulation(1, 2, 2);
            int matchedDay = 0;
            foreach (DayPopulation entry in dayPopulations)
            {
                if (entry.day >= matchedDay && entry.day <= Day)
                {
                    selected = entry;
                    matchedDay = entry.day;
                }
            }

            SetCustomerTarget(selected.customers);
            SetPedestrianTarget(selected.pedestrians);
        }

        public void SetCustomerTarget(int count) => CustomerTarget = Mathf.Clamp(count, 0, MaxCustomers);
        public void SetPedestrianTarget(int count) => PedestrianTarget = Mathf.Clamp(count, 0, MaxPopulation);
        public void AdjustCustomerTarget(int delta) => SetCustomerTarget(CustomerTarget + delta);
        public void AdjustPedestrianTarget(int delta) => SetPedestrianTarget(PedestrianTarget + delta);

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            SetAgentsStopped(customers, paused);
            SetAgentsStopped(pedestrians, paused);
        }

        public void GetQueueSnapshot(List<CheckoutWaitSnapshot> output) => checkoutQueue.CopySnapshot(output);

        private void Start()
        {
            if (!HasValidRoute())
            {
                Debug.LogError("NPC 테스트 씬의 프리팹 또는 이동 지점이 할당되지 않았습니다.", this);
                enabled = false;
                return;
            }

            SetDay(Day);
        }

        private bool HasValidRoute()
        {
            if (customerPrefab == null || !customerPrefab.TryGetComponent(out NavMeshAgent _) ||
                pedestrianPrefab == null || !pedestrianPrefab.TryGetComponent(out NavMeshAgent _) ||
                leftSidewalkZone == null || rightSidewalkZone == null || leftSidewalkZone == rightSidewalkZone ||
                sidewalkZoneSize.x <= 0f || sidewalkZoneSize.y <= 0f ||
                entrance == null || shelf == null || checkout == null || exit == null || queueSlots.Length == 0)
            {
                return false;
            }

            return Array.TrueForAll(queueSlots, point => point != null);
        }

        private void Update()
        {
            TrimPopulation(customers, CustomerTarget);
            TrimPopulation(pedestrians, PedestrianTarget);
            if (IsPaused)
                return;

            float elapsed = Time.deltaTime;
            checkoutQueue.TickWaiting(elapsed);

            TickPopulation(customers, elapsed);
            TickPopulation(pedestrians, elapsed);
            FillPopulation(customers, CustomerTarget, Role.Customer, customerPrefab, ref nextCustomerSpawn);
            FillPopulation(pedestrians, PedestrianTarget, Role.Pedestrian, pedestrianPrefab, ref nextPedestrianSpawn);
        }

        private void FillPopulation(List<AgentRun> group, int target, Role role, GameObject prefab,
            ref float nextAttempt)
        {
            if (group.Count >= target || Time.time < nextAttempt)
                return;

            nextAttempt = Time.time + Mathf.Max(0.25f, spawnInterval);
            NavMeshAgent template = prefab.GetComponent<NavMeshAgent>();
            var filter = NpcNavigation.FilterFor(template);
            bool fromLeft = UnityEngine.Random.value < 0.5f;
            Transform spawnZone = fromLeft ? leftSidewalkZone : rightSidewalkZone;
            Transform destinationZone = fromLeft ? rightSidewalkZone : leftSidewalkZone;
            if (!NpcNavigation.TrySampleZone(spawnZone.position, sidewalkZoneSize, filter, SpawnOccupied,
                    out Vector3 spawn, out bool hasWalkableSpawn))
            {
                if (!hasWalkableSpawn)
                    RouteFailures++;
                return;
            }

            if (!NpcNavigation.TrySampleZone(destinationZone.position, sidewalkZoneSize, filter, null,
                    out Vector3 destination, out _))
            {
                RouteFailures++;
                return;
            }

            var instance = Instantiate(prefab, spawn, spawnZone.rotation, transform);
            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            if (!agent.isOnNavMesh)
            {
                Dispose(agent);
                RouteFailures++;
                return;
            }

            int id = ++nextId;
            instance.name = role == Role.Customer ? $"손님 {id}" : $"행인 {id}";
            agent.avoidancePriority = 35 + id % 26;
            agent.stoppingDistance = Mathf.Min(agent.stoppingDistance, 0.1f);
            group.Add(new AgentRun(agent, role, id, destination));
        }

        private bool SpawnOccupied(Vector3 point) =>
            SpawnOccupied(point, customers) || SpawnOccupied(point, pedestrians);

        private static bool SpawnOccupied(Vector3 point, List<AgentRun> group)
        {
            foreach (AgentRun run in group)
            {
                if (run.Agent != null && (run.Agent.transform.position - point).sqrMagnitude < 1f)
                    return true;
            }

            return false;
        }

        private void TickPopulation(List<AgentRun> group, float elapsed)
        {
            int i = 0;
            while (i < group.Count)
            {
                AgentRun run = group[i];
                Result result = run.Agent == null ? Result.Failure : TickSequence(run, elapsed);
                if (result == Result.Running)
                {
                    i++;
                    continue;
                }

                if (result == Result.Failure)
                    RouteFailures++;
                else if (run.Role == Role.Customer)
                    CompletedCustomers++;
                else
                    CompletedPedestrians++;
                RemoveAgent(group, i);
            }
        }

        private Result TickSequence(AgentRun run, float elapsed)
        {
            Result result = TickStep(run, run.Sequence[run.StepIndex], elapsed);
            if (result != Result.Success)
                return result;

            run.Navigation.ClearTarget();
            run.BrowseSeconds = 0f;
            run.StepIndex++;
            return run.StepIndex == run.Sequence.Length ? Result.Success : Result.Running;
        }

        private Result TickStep(AgentRun run, Step step, float elapsed)
        {
            switch (step)
            {
                case Step.Enter:
                    return run.Navigation.MoveTo(run.Agent, entrance, elapsed);
                case Step.Browse:
                    return PickUpProduct(run, elapsed);
                case Step.Queue:
                    return WaitInLine(run, elapsed);
                case Step.Checkout:
                    return CheckOut(run, elapsed);
                case Step.Exit:
                    return run.Navigation.MoveTo(run.Agent, exit, elapsed);
                case Step.Outside:
                case Step.WalkSidewalk:
                    return run.Navigation.MoveTo(run.Agent, run.SidewalkDestination, elapsed);
                default:
                    return Result.Failure;
            }
        }

        private Result PickUpProduct(AgentRun customer, float elapsed)
        {
            Result movement = customer.Navigation.MoveTo(customer.Agent, shelf, elapsed);
            if (movement != Result.Success)
                return movement;

            customer.BrowseSeconds += elapsed;
            if (customer.BrowseSeconds < browseDuration)
                return Result.Running;

            if (productPrefab != null)
            {
                customer.CarriedItem = Instantiate(productPrefab, customer.Agent.transform, false);
                customer.CarriedItem.transform.SetLocalPositionAndRotation(
                    new Vector3(0.28f, 0.88f, 0.48f), Quaternion.identity);
            }

            return Result.Success;
        }

        private Result WaitInLine(AgentRun customer, float elapsed)
        {
            int position = checkoutQueue.ClaimPosition(customer.Id);
            if (position == NpcCheckoutQueue.RegisterPosition)
                return Result.Success;

            Result movement = customer.Navigation.MoveTo(customer.Agent, queueSlots[position], elapsed);
            return movement == Result.Failure ? Result.Failure : Result.Running;
        }

        private Result CheckOut(AgentRun customer, float elapsed)
        {
            Result movement = customer.Navigation.MoveTo(customer.Agent, checkout, elapsed);
            if (movement != Result.Success)
                return movement;

            if (!checkoutQueue.TickService(customer.Id, elapsed, checkoutDuration))
                return Result.Running;

            SuccessfulMockCheckouts++;
            if (customer.CarriedItem != null)
            {
                customer.CarriedItem.SetActive(false);
                Destroy(customer.CarriedItem);
                customer.CarriedItem = null;
            }
            return Result.Success;
        }

        private void TrimPopulation(List<AgentRun> group, int target)
        {
            while (group.Count > target)
                RemoveAgent(group, group.Count - 1);
        }

        private void RemoveAgent(List<AgentRun> group, int index)
        {
            AgentRun run = group[index];
            if (run.Role == Role.Customer)
                checkoutQueue.Release(run.Id);
            group.RemoveAt(index);
            Dispose(run.Agent);
        }

        private static void Dispose(NavMeshAgent agent)
        {
            if (agent == null)
                return;

            agent.gameObject.SetActive(false);
            Destroy(agent.gameObject);
        }

        private static void SetAgentsStopped(List<AgentRun> group, bool stopped)
        {
            foreach (AgentRun run in group)
            {
                if (run.Agent != null && run.Agent.isOnNavMesh)
                    run.Agent.isStopped = stopped;
            }
        }

        private void OnDisable()
        {
            TrimPopulation(customers, 0);
            TrimPopulation(pedestrians, 0);
        }
    }
}
