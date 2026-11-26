using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Result = OpenSourceTeam6.Customers.Unity.NpcStepResult;

namespace OpenSourceTeam6.Customers.Unity
{
    public enum ProductCategory { Drink, Snack, Ramen, Daily, IceCream, FreshFood, Cigarette }

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
        private enum ShoppingStyle { Direct, Deliberating, Indecisive, AskWhenMissing, LeaveWhenMissing, HesitateAndLeave }
        private enum Step { Enter, Consider, Browse, Queue, Checkout, Sit, Exit, Outside, WalkSidewalk }

        private static readonly Step[] CustomerSequence =
        {
            Step.Enter, Step.Consider, Step.Browse, Step.Sit, Step.Queue, Step.Checkout, Step.Exit, Step.Outside
        };

        [Header("손님 좌석")]
        [SerializeField] private Transform customerSeat;
        [SerializeField, Min(0f)] private float customerSeatedSeconds = 2f;
        private AgentRun seatedCustomer;

        private static readonly Step[] PedestrianSequence = { Step.WalkSidewalk };

        private sealed class AgentRun
        {
            public readonly NavMeshAgent Agent;
            public readonly Role Role;
            public readonly int Id;
            public readonly Step[] Sequence;
            public readonly Vector3 SidewalkDestination;
            public readonly NpcNavigation Navigation = new NpcNavigation();
            public readonly NpcCharacterAnimation Animation;
            public readonly bool IsRunner;
            public int StepIndex;
            public float BrowseSeconds;
            public float SitSeconds;
            public ShoppingStyle ShoppingStyle;
            public int ProductIndex = -1;
            public int AisleIndex;
            public float ConsiderSeconds;
            public float RequiredConsiderSeconds;
            public float ShelfWaitSeconds;
            public float ShelfWaitDuration;
            public int DesiredProductIndex = -1;
            public NpcSearchOrder SearchOrder;
            public bool SeekingHelp;
            public Transform QuestionTarget;
            public bool QuestionAsked;
            public float QuestionSeconds;
            public Transform AnswerLocation;
            public ProductOption Product;
            public NpcCarriedProduct CarriedProduct;
            public int LastAisleIndex = -1;
            public Vector3? ConsiderLookTarget;
            public bool AtCheckoutCounter;
            public float CheckoutWaitSeconds;

            public AgentRun(NavMeshAgent agent, Role role, int id, Vector3 sidewalkDestination, bool isRunner)
            {
                Agent = agent;
                IsRunner = isRunner;
                Animation = agent.GetComponentInChildren<NpcCharacterAnimation>();
                Role = role;
                Id = id;
                SidewalkDestination = sidewalkDestination;
                Sequence = role == Role.Customer ? CustomerSequence : PedestrianSequence;
            }
        }

        [Serializable]
        private struct ProductOption
        {
            public ProductCategory category;
            public string productName;
            public Transform pickupLocation;
            public GameObject visualPrefab;
            public GameObject shelfItem;
            public bool unstocked;
            public Transform shelfLookTarget;
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
        [SerializeField] private NpcCharacterRoster characterRoster;
        [SerializeField, Range(0f, 1f)] private float customerRunChance = 0.3f;
        [SerializeField, Range(0f, 1f)] private float pedestrianRunChance = 0.3f;
        [SerializeField] private GameObject productPrefab;
        [Header("판매 상품 후보")]
        [SerializeField] private ProductOption[] products =
        {
            new ProductOption { category = ProductCategory.Drink, productName = "P_RSG_WaterBottle" },
            new ProductOption { category = ProductCategory.Snack, productName = "Chocolate_Bar_1" },
            new ProductOption { category = ProductCategory.Ramen, productName = "SM_Cook_PhoBowl_Full_01" },
            new ProductOption { category = ProductCategory.Daily, productName = "SM_Cook_NapkinBox_01" },
            new ProductOption { category = ProductCategory.IceCream, productName = "Vanilla_Ice_Cream_1" },
            new ProductOption { category = ProductCategory.FreshFood, productName = "Hambuger" },
            new ProductOption { category = ProductCategory.Cigarette, productName = "CigPack" }
        };
        [Header("공용 보도 출입 구역")]
        [SerializeField] private Transform leftSidewalkZone;
        [SerializeField] private Transform rightSidewalkZone;
        [SerializeField] private Vector2 sidewalkZoneSize = new Vector2(4f, 4.8f);
        [Header("손님 동선")]
        [SerializeField] private Transform entrance;
        [SerializeField] private Transform[] browsingWaypoints = Array.Empty<Transform>();
        [SerializeField] private Transform shelf;
        [SerializeField] private Transform checkout;
        [SerializeField] private Transform checkoutLookTarget;
        [SerializeField] private Transform checkoutItemSpot;
        [SerializeField] private Transform[] queueSlots = Array.Empty<Transform>();
        [SerializeField] private Transform exit;
        [Header("상품 위치 질문 대상 (선택)")]
        [SerializeField] private Transform employeeQuestionTarget;
        [SerializeField] private Transform playerQuestionTarget;
        [SerializeField, Min(1f)] private float questionTimeout = 15f;
        [SerializeField] private string demoMissingProductName;
        [Header("테스트 시간")]
        [SerializeField, Min(0.25f)] private float spawnInterval = 1f;
        [SerializeField, Min(0)] private float browseDuration = 0.6f;
        [SerializeField, Min(0.5f)] private float hesitationMinSeconds = 2f;
        [SerializeField, Min(0.5f)] private float hesitationMaxSeconds = 4f;
        [SerializeField, Min(0.1f)] private float checkoutDuration = 3f;

        private readonly List<AgentRun> customers = new List<AgentRun>(8);
        private readonly List<AgentRun> pedestrians = new List<AgentRun>(8);
        private readonly NpcCheckoutQueue checkoutQueue = new NpcCheckoutQueue();
        [Header("결제 연동")]
        [SerializeField] private bool waitForExternalCheckout;
        private int pendingCheckoutId = -1;
        private string pendingCheckoutTag;
        private bool pendingCheckoutApproved;
        private float nextCustomerSpawn;
        private NpcSearchOrder shoppingStyles;
        private float nextPedestrianSpawn;
        private int nextId;
        private float customerSpeed = 1.5f;
        private float pedestrianSpeed = 1.7f;

        public const float MinimumMoveSpeed = 0.5f;
        public const float MaximumMoveSpeed = 3f;
        private const float MinimumCustomerRespawnDelay = 1f;
        private const float MaximumCustomerRespawnDelay = 5f;
        private const float RunnerSpeedMultiplier = 1.65f;
        private const float PickupAnimationSeconds = 0.85f;
        private const float QuestionAnimationSeconds = 0.9f;

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
        public float CustomerSpeed => customerSpeed;
        public float PedestrianSpeed => pedestrianSpeed;
        public event Action<int, string, Transform> CustomerAskedForProduct;

        public bool TryAnswerProductQuestion(int customerId, Transform productLocation)
        {
            if (productLocation == null) return false;
            foreach (AgentRun customer in customers)
            {
                if (customer.Id != customerId || customer.Sequence[customer.StepIndex] != Step.Consider ||
                    !customer.QuestionAsked || customer.AnswerLocation != null)
                    continue;
                customer.AnswerLocation = productLocation;
                return true;
            }
            return false;
        }

        // 선반 재고를 조회만 한다. 실제 재고 차감·매출 반영은 외부 시스템의 책임이다.
        public Func<string, bool> CanTakeFromShelf { get; set; }
        public event Action<int, string> ProductPickedUp;
        public bool WaitForExternalCheckout { get => waitForExternalCheckout; set => waitForExternalCheckout = value; }
        public Transform CheckoutLookTarget { get => checkoutLookTarget; set => checkoutLookTarget = value; }
        public Transform CheckoutItemSpot { get => checkoutItemSpot; set => checkoutItemSpot = value; }

        public static string GetProductTag(ProductCategory category)
        {
            switch (category)
            {
                case ProductCategory.Drink: return "Item_Drink";
                case ProductCategory.Snack: return "Item_Snack";
                case ProductCategory.Ramen: return "Item_Ramen";
                case ProductCategory.Daily: return "Item_Daily";
                case ProductCategory.IceCream: return "Item_IceCream";
                case ProductCategory.FreshFood: return "Item_FreshFood";
                case ProductCategory.Cigarette: return "Item_Cigarette";
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        public bool TryGetCurrentCheckout(out int customerId, out string productTag)
        {
            customerId = pendingCheckoutId;
            productTag = pendingCheckoutTag;
            return waitForExternalCheckout && pendingCheckoutId >= 0 && !pendingCheckoutApproved;
        }

        public bool TryCompleteCurrentCheckout(out int customerId)
        {
            customerId = -1;
            if (!TryCompleteCurrentCheckout(pendingCheckoutId)) return false;
            customerId = pendingCheckoutId;
            return true;
        }

        public bool TryCompleteCurrentCheckout(int expectedCustomerId)
        {
            if (IsPaused || !waitForExternalCheckout || expectedCustomerId < 0 ||
                pendingCheckoutId != expectedCustomerId || pendingCheckoutApproved)
                return false;
            pendingCheckoutApproved = true;
            return true;
        }

        public void SetCustomerSpeed(float speed) => SetSpeed(customers, ref customerSpeed, speed);
        public void SetPedestrianSpeed(float speed) => SetSpeed(pedestrians, ref pedestrianSpeed, speed);

        private static void SetSpeed(List<AgentRun> group, ref float currentSpeed, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return;
            currentSpeed = Mathf.Clamp(value, MinimumMoveSpeed, MaximumMoveSpeed);
            foreach (AgentRun run in group)
                if (run.Agent != null)
                    run.Agent.speed = GetTravelSpeed(currentSpeed, run.IsRunner);
        }

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
            float runChance = role == Role.Customer ? customerRunChance : pedestrianRunChance;
            bool isRunner = runChance > 0f && (runChance >= 1f || UnityEngine.Random.value < runChance);
            agent.speed = GetTravelSpeed(role == Role.Customer ? customerSpeed : pedestrianSpeed, isRunner);
            instance.name += isRunner ? " (뛰기)" : " (걷기)";
            if (characterRoster != null)
                characterRoster.ApplyRandom(instance.transform, instance.GetComponentInChildren<NpcCharacterAnimation>());
            var run = new AgentRun(agent, role, id, destination, isRunner);
            if (role == Role.Customer)
            {
                AssignShoppingStyle(run);
                instance.name += $" ({run.ShoppingStyle})";
            }
            group.Add(run);
            if (role == Role.Customer)
                ScheduleNextCustomerSpawn();
        }

        private void AssignShoppingStyle(AgentRun customer)
        {
            // Shuffle each group so all behaviors appear even in short test runs.
            if (shoppingStyles == null || shoppingStyles.IsComplete)
                shoppingStyles = CreateSearchOrder((int)ShoppingStyle.HesitateAndLeave + 1);
            shoppingStyles.TryGetCurrent(out int index);
            shoppingStyles.Advance();
            customer.ShoppingStyle = (ShoppingStyle)index;

            if (customer.ShoppingStyle == ShoppingStyle.Deliberating || customer.ShoppingStyle == ShoppingStyle.Indecisive ||
                customer.ShoppingStyle == ShoppingStyle.HesitateAndLeave)
            {
                if (customer.ShoppingStyle == ShoppingStyle.Deliberating && browsingWaypoints.Length > 0)
                    customer.AisleIndex = UnityEngine.Random.Range(0, Mathf.Min(2, browsingWaypoints.Length));
                customer.RequiredConsiderSeconds = UnityEngine.Random.Range(hesitationMinSeconds,
                    Mathf.Max(hesitationMinSeconds, hesitationMaxSeconds));
                if (customer.ShoppingStyle == ShoppingStyle.Indecisive)
                {
                    if (browsingWaypoints.Length > 0)
                        customer.AisleIndex = UnityEngine.Random.Range(browsingWaypoints.Length / 2, browsingWaypoints.Length);
                    customer.RequiredConsiderSeconds *= 2f;
                }
            }
        }

        private static NpcSearchOrder CreateSearchOrder(int count) =>
            new NpcSearchOrder(count, new System.Random(UnityEngine.Random.Range(0, int.MaxValue)));

        private void ScheduleNextCustomerSpawn()
        {
            nextCustomerSpawn = Time.time + UnityEngine.Random.Range(
                MinimumCustomerRespawnDelay, MaximumCustomerRespawnDelay);
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
                {
                    CompletedCustomers++;
                    // The spawn pass runs after this one, so start the delay before removing the customer.
                    ScheduleNextCustomerSpawn();
                }
                else
                    CompletedPedestrians++;
                RemoveAgent(group, i);
            }
        }

        private Result TickSequence(AgentRun run, float elapsed)
        {
            Step step = run.Sequence[run.StepIndex];
            Result result = TickStep(run, step, elapsed);
            if (result != Result.Failure)
                UpdateAnimation(run, step, result);
            if (result != Result.Success)
                return result;

            run.Navigation.ClearTarget();
            run.BrowseSeconds = 0f;
            run.ConsiderLookTarget = null;
            run.AtCheckoutCounter = false;
            run.CheckoutWaitSeconds = 0f;
            run.StepIndex++;
            return run.StepIndex == run.Sequence.Length ? Result.Success : Result.Running;
        }

        private void UpdateAnimation(AgentRun run, Step step, Result result)
        {
            var presentation = run.Animation;
            if (presentation == null)
                return;

            Vector3 velocity = run.Agent.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;

            if (result == Result.Running)
            {
                if (step == Step.Sit)
                    return;
                if (step == Step.Browse && run.BrowseSeconds > 0f)
                    return; // The pickup step updates the reach target and progress.
                if (step == Step.Consider)
                {
                    if (run.SeekingHelp && run.QuestionAsked)
                    {
                        Vector3? qTarget = run.QuestionTarget != null
                            ? (Vector3?)(run.QuestionTarget.position + Vector3.up * 1.4f)
                            : null;
                        presentation.Show(NpcAnimationState.Ask, qTarget);
                        return;
                    }

                    if (speed <= 0.08f && (run.ConsiderSeconds > 0f || run.ShelfWaitSeconds > 0f))
                    {
                        presentation.Show(NpcAnimationState.Consider, run.ConsiderLookTarget);
                        return;
                    }
                }
                if (step == Step.Checkout && run.AtCheckoutCounter)
                {
                    presentation.Show(NpcAnimationState.Wait, GetCheckoutCounterLookTarget());
                    return;
                }
            }

            if (speed > 0.08f)
                presentation.Show(run.IsRunner ? NpcAnimationState.Run : NpcAnimationState.Walk);
            else
                presentation.Show(run.Role == Role.Customer ? NpcAnimationState.Wait : NpcAnimationState.Idle);
        }

        private static Vector3? GetProductLookTarget(ProductOption option)
        {
            if (option.shelfItem != null)
                return option.shelfItem.transform.position;
            if (option.shelfLookTarget != null)
                return option.shelfLookTarget.position;
            return null;
        }

        private Vector3? GetCheckoutCounterLookTarget()
        {
            if (checkoutLookTarget != null)
                return checkoutLookTarget.position;
            if (checkout != null)
                return checkout.position + checkout.forward * 1.5f + Vector3.up * 1.35f;
            return null;
        }

        private static float GetTravelSpeed(float walkSpeed, bool isRunner) =>
            isRunner ? walkSpeed * RunnerSpeedMultiplier : walkSpeed;

        private static void FaceInteraction(NavMeshAgent agent, Vector3 target, float elapsed)
        {
            Vector3 direction = target - agent.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0025f)
                agent.transform.rotation = Quaternion.RotateTowards(agent.transform.rotation,
                    Quaternion.LookRotation(direction), 540f * elapsed);
        }

        private Result TickStep(AgentRun run, Step step, float elapsed)
        {
            switch (step)
            {
                case Step.Enter:
                    Result entranceResult = run.Navigation.MoveTo(run.Agent, entrance, elapsed);
                    if (entranceResult == Result.Success && run.ShoppingStyle == ShoppingStyle.HesitateAndLeave)
                    {
                        run.Agent.isStopped = true;
                        run.Agent.velocity = Vector3.zero;
                    }
                    return entranceResult;
                case Step.Consider:
                    return ConsiderPurchase(run, elapsed);
                case Step.Browse:
                    return PickUpProduct(run, elapsed);
                case Step.Queue:
                    return WaitInLine(run, elapsed);
                case Step.Checkout:
                    return CheckOut(run, elapsed);
                case Step.Sit:
                    return SitBeforeCheckout(run, elapsed);
                case Step.Exit:
                    return run.Navigation.MoveTo(run.Agent, exit, elapsed);
                case Step.Outside:
                case Step.WalkSidewalk:
                    return run.Navigation.MoveTo(run.Agent, run.SidewalkDestination, elapsed);
                default:
                    return Result.Failure;
            }
        }

        private Result SitBeforeCheckout(AgentRun run, float elapsed)
        {
            if (seatedCustomer != run)
            {
                if (seatedCustomer != null || customerSeat == null || customerSeatedSeconds <= 0f ||
                    run.Animation == null || !run.Animation.CanShow(NpcAnimationState.SitDown) ||
                    !run.Animation.CanShow(NpcAnimationState.Seated) || !run.Agent.isOnNavMesh ||
                    !run.Agent.Warp(customerSeat.position))
                    return Result.Success;

                seatedCustomer = run;
                run.Agent.ResetPath();
                run.Agent.transform.rotation = customerSeat.rotation;
            }

            run.SitSeconds += elapsed;
            float sitDownSeconds = NpcCharacterAnimation.SitDownSeconds;
            if (run.SitSeconds >= sitDownSeconds + customerSeatedSeconds)
            {
                seatedCustomer = null;
                return Result.Success;
            }

            run.Animation.Show(run.SitSeconds < sitDownSeconds ? NpcAnimationState.SitDown : NpcAnimationState.Seated);
            return Result.Running;
        }


        private Result ConsiderPurchase(AgentRun customer, float elapsed)
        {
            if (products == null || products.Length == 0)
                return LeaveWithoutPurchase(customer);

            if (customer.SeekingHelp)
                return AskForProduct(customer, elapsed);

            switch (customer.ShoppingStyle)
            {
                case ShoppingStyle.Direct:
                    return ChooseDisplayedProduct(customer);
                case ShoppingStyle.HesitateAndLeave:
                    customer.ConsiderSeconds += elapsed;
                    if (customer.ConsiderSeconds < customer.RequiredConsiderSeconds)
                    {
                        if (shelf != null)
                        {
                            FaceInteraction(customer.Agent, shelf.position, elapsed);
                            customer.ConsiderLookTarget = shelf.position + Vector3.up * 1.35f;
                        }
                        else
                        {
                            customer.ConsiderLookTarget = customer.Agent.transform.position +
                                customer.Agent.transform.forward * 2f + Vector3.up * 1.35f;
                        }
                        return Result.Running;
                    }
                    if (!customer.Agent.isOnNavMesh)
                        return Result.Failure;
                    customer.Agent.isStopped = false;
                    return LeaveWithoutPurchase(customer);
                case ShoppingStyle.Deliberating:
                    return PauseAndChooseProduct(customer, elapsed);
                case ShoppingStyle.Indecisive:
                    return ConsiderMultipleProducts(customer, elapsed);
                case ShoppingStyle.AskWhenMissing:
                case ShoppingStyle.LeaveWhenMissing:
                    return SearchShelves(customer, elapsed);
                default:
                    return LeaveWithoutPurchase(customer);
            }
        }

        private Result ConsiderMultipleProducts(AgentRun customer, float elapsed)
        {
            if (customer.SearchOrder == null)
                customer.SearchOrder = CreateSearchOrder(products.Length);
            if (!customer.SearchOrder.TryGetCurrent(out int index))
                return ChooseDisplayedProduct(customer);

            var product = products[index];
            Transform location = product.pickupLocation != null ? product.pickupLocation : shelf;
            Result movement = customer.Navigation.MoveTo(customer.Agent, location, elapsed);
            if (movement == Result.Failure)
            {
                customer.SearchOrder.Advance();
                customer.Navigation.ClearTarget();
                return Result.Running;
            }
            if (movement != Result.Success)
            {
                customer.ConsiderLookTarget = null;
                return Result.Running;
            }

            if (customer.ShelfWaitDuration <= 0f)
            {
                customer.ShelfWaitDuration = UnityEngine.Random.Range(1f, 5f);
                if (customer.Agent.isOnNavMesh)
                {
                    customer.Agent.isStopped = true;
                    customer.Agent.velocity = Vector3.zero;
                }
            }
            customer.ShelfWaitSeconds += elapsed;
            if (customer.ShelfWaitSeconds < customer.ShelfWaitDuration)
            {
                Vector3? lookTarget = GetProductLookTarget(product);
                customer.ConsiderLookTarget = lookTarget;
                if (lookTarget.HasValue)
                    FaceInteraction(customer.Agent, lookTarget.Value, elapsed);
                return Result.Running;
            }

            if (customer.Agent.isOnNavMesh)
                customer.Agent.isStopped = false;
            customer.SearchOrder.Advance();
            customer.Navigation.ClearTarget();
            customer.ShelfWaitSeconds = 0f;
            customer.ShelfWaitDuration = 0f;
            if (customer.SearchOrder.VisitedCount >= Mathf.Min(3, products.Length))
                return ChooseDisplayedProduct(customer);
            return Result.Running;
        }

        private Result ChooseDisplayedProduct(AgentRun customer)
        {
            int start = UnityEngine.Random.Range(0, products.Length);
            for (int offset = 0; offset < products.Length; offset++)
            {
                int index = (start + offset) % products.Length;
                if (IsProductInStock(products[index]) && TryReserveSpecificProduct(customer, index))
                    return Result.Success;
            }

            return LeaveWithoutPurchase(customer);
        }

        private Result PauseAndChooseProduct(AgentRun customer, float elapsed)
        {
            if (customer.AisleIndex < browsingWaypoints.Length)
            {
                Result movement = customer.Navigation.MoveTo(
                    customer.Agent, browsingWaypoints[customer.AisleIndex], elapsed);
                if (movement == Result.Running)
                {
                    customer.ConsiderLookTarget = null;
                    return Result.Running;
                }

                customer.LastAisleIndex = customer.AisleIndex;
                customer.AisleIndex = browsingWaypoints.Length;
                customer.Navigation.ClearTarget();
                if (customer.Agent.isOnNavMesh)
                {
                    if (customer.Agent.hasPath)
                        customer.Agent.ResetPath();
                    customer.Agent.velocity = Vector3.zero;
                }
            }

            customer.ConsiderSeconds += elapsed;
            if (customer.ConsiderSeconds < customer.RequiredConsiderSeconds)
            {
                if (customer.LastAisleIndex >= 0 && customer.LastAisleIndex < browsingWaypoints.Length &&
                    browsingWaypoints[customer.LastAisleIndex] != null)
                {
                    Transform wp = browsingWaypoints[customer.LastAisleIndex];
                    Vector3 lookPos = wp.position + wp.forward * 2.0f + Vector3.up * 1.25f;
                    FaceInteraction(customer.Agent, lookPos, elapsed);
                    customer.ConsiderLookTarget = lookPos;
                }
                else if (shelf != null)
                {
                    FaceInteraction(customer.Agent, shelf.position, elapsed);
                    customer.ConsiderLookTarget = shelf.position + Vector3.up * 1.25f;
                }
                return Result.Running;
            }

            return ChooseDisplayedProduct(customer);
        }

        private Result SearchShelves(AgentRun customer, float elapsed)
        {
            if (customer.SearchOrder == null)
            {
                customer.SearchOrder = CreateSearchOrder(products.Length);
                customer.DesiredProductIndex = ChooseSearchTarget();
            }

            if (!customer.SearchOrder.TryGetCurrent(out int index))
                return HandleMissingProduct(customer);

            ProductOption option = products[index];
            Transform location = option.pickupLocation != null ? option.pickupLocation : shelf;
            Result movement = customer.Navigation.MoveTo(customer.Agent, location, elapsed);
            if (movement == Result.Running)
            {
                customer.ConsiderLookTarget = null;
                return Result.Running;
            }

            if (movement == Result.Success && customer.ShoppingStyle == ShoppingStyle.LeaveWhenMissing)
            {
                if (customer.ShelfWaitDuration <= 0f)
                {
                    customer.ShelfWaitDuration = UnityEngine.Random.Range(1f, 5f);
                    if (customer.Agent.isOnNavMesh)
                    {
                        customer.Agent.isStopped = true;
                        customer.Agent.velocity = Vector3.zero;
                    }
                }
                customer.ShelfWaitSeconds += elapsed;
                if (customer.ShelfWaitSeconds < customer.ShelfWaitDuration)
                {
                    Vector3? lookTarget = GetProductLookTarget(option);
                    customer.ConsiderLookTarget = lookTarget;
                    if (lookTarget.HasValue)
                        FaceInteraction(customer.Agent, lookTarget.Value, elapsed);
                    return Result.Running;
                }
                if (customer.Agent.isOnNavMesh)
                    customer.Agent.isStopped = false;
                customer.ShelfWaitSeconds = 0f;
                customer.ShelfWaitDuration = 0f;
            }

            customer.SearchOrder.Advance();
            customer.Navigation.ClearTarget();
            if (movement == Result.Success && index == customer.DesiredProductIndex &&
                IsProductInStock(option) && TryReserveSpecificProduct(customer, index))
                return Result.Success;

            return Result.Running;
        }

        private int ChooseSearchTarget()
        {
            if (!string.IsNullOrEmpty(demoMissingProductName))
            {
                int demo = Array.FindIndex(products, item => item.unstocked && item.productName == demoMissingProductName);
                if (demo >= 0) return demo;
            }

            return UnityEngine.Random.Range(0, products.Length);
        }

        private bool IsProductInStock(ProductOption option) =>
            !option.unstocked && (option.shelfItem == null || option.shelfItem.activeInHierarchy) &&
            (CanTakeFromShelf == null || CanTakeFromShelf(GetProductTag(option.category)));

        private Result HandleMissingProduct(AgentRun customer)
        {
            if (customer.ShoppingStyle != ShoppingStyle.AskWhenMissing || customer.QuestionAsked)
                return LeaveWithoutPurchase(customer);

            customer.SeekingHelp = true;
            return Result.Running;
        }

        private static Result LeaveWithoutPurchase(AgentRun customer)
        {
            customer.StepIndex = Array.IndexOf(customer.Sequence, Step.Exit) - 1;
            return Result.Success;
        }

        private Result AskForProduct(AgentRun customer, float elapsed)
        {
            if (customer.QuestionTarget == null && !customer.QuestionAsked)
            {
                if (employeeQuestionTarget == null) customer.QuestionTarget = playerQuestionTarget;
                else if (playerQuestionTarget == null) customer.QuestionTarget = employeeQuestionTarget;
                else customer.QuestionTarget =
                    (employeeQuestionTarget.position - customer.Agent.transform.position).sqrMagnitude <=
                    (playerQuestionTarget.position - customer.Agent.transform.position).sqrMagnitude
                        ? employeeQuestionTarget : playerQuestionTarget;
                if (customer.QuestionTarget == null)
                    return LeaveWithoutPurchase(customer);
            }
            if (!customer.QuestionAsked)
            {
                Result movement = customer.Navigation.MoveTo(customer.Agent, customer.QuestionTarget, elapsed);
                if (movement == Result.Failure)
                    return LeaveWithoutPurchase(customer);
                if (movement != Result.Success) return Result.Running;
                customer.QuestionAsked = true;
                CustomerAskedForProduct?.Invoke(customer.Id, products[customer.DesiredProductIndex].productName,
                    customer.QuestionTarget);
            }
            float minimumQuestionTime = customer.Animation != null ? Mathf.Min(questionTimeout, QuestionAnimationSeconds) : 0f;
            if (customer.Animation != null && customer.QuestionTarget != null)
                FaceInteraction(customer.Agent, customer.QuestionTarget.position, elapsed);
            if (customer.QuestionSeconds >= minimumQuestionTime &&
                customer.AnswerLocation != null && IsProductInStock(products[customer.DesiredProductIndex]) &&
                TryReserveSpecificProduct(customer, customer.DesiredProductIndex))
            {
                ProductOption product = customer.Product;
                product.pickupLocation = customer.AnswerLocation;
                customer.Product = product;
                return Result.Success;
            }
            customer.QuestionSeconds += elapsed;
            if (customer.QuestionSeconds < questionTimeout) return Result.Running;
            return LeaveWithoutPurchase(customer);
        }

        private bool TryReserveSpecificProduct(AgentRun customer, int index)
        {
            ProductOption option = products[index];
            foreach (AgentRun other in customers)
            {
                if (other == customer || other.ProductIndex < 0) continue;
                if (other.ProductIndex == index ||
                    (option.shelfItem != null && option.shelfItem == other.Product.shelfItem) ||
                    (!string.IsNullOrEmpty(option.productName) && option.productName == other.Product.productName))
                    return false;
            }
            customer.ProductIndex = index;
            customer.Product = option;
            return true;
        }

        private Result PickUpProduct(AgentRun customer, float elapsed)
        {
            Transform pickup = customer.Product.pickupLocation != null ? customer.Product.pickupLocation : shelf;
            Result movement = customer.Navigation.MoveTo(customer.Agent, pickup, elapsed);
            if (movement != Result.Success)
                return movement;

            float duration = customer.Animation != null ? Mathf.Max(browseDuration, PickupAnimationSeconds) : browseDuration;
            const float pointSeconds = 0.85f;
            Vector3? target = GetProductLookTarget(customer.Product);
            bool pointBeforePickup = customer.Animation != null && target.HasValue &&
                customer.Product.category == ProductCategory.Cigarette;
            customer.BrowseSeconds += elapsed;
            if (customer.Animation != null)
            {
                if (target.HasValue)
                    FaceInteraction(customer.Agent, target.Value, elapsed);
                if (pointBeforePickup && customer.BrowseSeconds < pointSeconds)
                {
                    customer.Animation.Show(NpcAnimationState.Point, target);
                    return Result.Running;
                }

                float pickupElapsed = customer.BrowseSeconds - (pointBeforePickup ? pointSeconds : 0f);
                customer.Animation.Show(NpcAnimationState.Pickup, target,
                    Mathf.Clamp01(pickupElapsed / duration) * 0.6f);
            }
            if (customer.BrowseSeconds < duration + (pointBeforePickup ? pointSeconds : 0f))
                return Result.Running;

            if (!IsProductInStock(customer.Product))
            {
                customer.ProductIndex = -1;
                if (customer.ShoppingStyle == ShoppingStyle.AskWhenMissing && !customer.QuestionAsked)
                {
                    customer.SeekingHelp = true;
                    customer.StepIndex = Array.IndexOf(customer.Sequence, Step.Consider) - 1;
                    return Result.Success;
                }
                return LeaveWithoutPurchase(customer);
            }

            GameObject visual = customer.Product.visualPrefab != null ? customer.Product.visualPrefab : productPrefab;
            customer.CarriedProduct = NpcCarriedProduct.Attach(customer.Product.shelfItem, visual,
                customer.Agent.transform, customer.Animation != null ? customer.Animation.RightHand : null,
                customer.Animation);

            ProductPickedUp?.Invoke(customer.Id, GetProductTag(customer.Product.category));
            return Result.Success;
        }

        private Result WaitInLine(AgentRun customer, float elapsed)
        {
            int position = checkoutQueue.ClaimPosition(customer.Id);
            if (position == NpcCheckoutQueue.RegisterPosition)
                return Result.Success;

            Result movement = customer.Navigation.MoveTo(customer.Agent, queueSlots[position], elapsed);
            if (movement == Result.Success)
            {
                Transform ahead = position == 0 ? checkout : queueSlots[position - 1];
                if (ahead != null)
                    FaceInteraction(customer.Agent, ahead.position, elapsed);
            }
            return movement == Result.Failure ? Result.Failure : Result.Running;
        }

        private Result CheckOut(AgentRun customer, float elapsed)
        {
            Result movement = customer.Navigation.MoveTo(customer.Agent, checkout, elapsed);
            if (movement != Result.Success)
            {
                customer.AtCheckoutCounter = false;
                return movement;
            }

            customer.AtCheckoutCounter = true;
            customer.CheckoutWaitSeconds += elapsed;
            Vector3? lookTarget = GetCheckoutCounterLookTarget();
            if (lookTarget.HasValue)
                FaceInteraction(customer.Agent, lookTarget.Value, elapsed);

            if (checkoutItemSpot != null && customer.CarriedProduct != null && !customer.CarriedProduct.IsOnCounter)
            {
                customer.CarriedProduct.PlaceOnCounter(checkoutItemSpot);
            }

            if (waitForExternalCheckout)
            {
                if (pendingCheckoutId < 0)
                {
                    pendingCheckoutId = customer.Id;
                    pendingCheckoutTag = GetProductTag(customer.Product.category);
                    pendingCheckoutApproved = false;
                }
                if (pendingCheckoutId != customer.Id || !pendingCheckoutApproved)
                    return Result.Running;
                if (!checkoutQueue.TickService(customer.Id, checkoutDuration, checkoutDuration))
                    return Result.Running;
            }
            else if (!checkoutQueue.TickService(customer.Id, elapsed, checkoutDuration))
                return Result.Running;

            ClearCheckoutApproval(customer.Id);
            SuccessfulMockCheckouts++;
            if (customer.CarriedProduct != null)
            {
                if (customer.CarriedProduct.IsOnCounter)
                {
                    customer.CarriedProduct.ReturnToHand(
                        customer.Agent.transform,
                        customer.Animation != null ? customer.Animation.RightHand : null,
                        customer.Animation);
                }

                // Standalone test scenes without a counter spot that borrow a live shelf item still return it to display immediately.
                if (customer.CarriedProduct.ReturnsToDisplay && checkoutItemSpot == null)
                {
                    ReleaseCarriedProduct(customer);
                }
            }
            customer.ProductIndex = -1;
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
            if (seatedCustomer == run) seatedCustomer = null;
            ClearCheckoutApproval(run.Id);
            ReleaseCarriedProduct(run);
            if (run.Role == Role.Customer)
                checkoutQueue.Release(run.Id);
            group.RemoveAt(index);
            Dispose(run.Agent);
        }

        private void ClearCheckoutApproval(int customerId)
        {
            if (pendingCheckoutId != customerId)
                return;

            pendingCheckoutId = -1;
            pendingCheckoutTag = null;
            pendingCheckoutApproved = false;
        }

        private static void ReleaseCarriedProduct(AgentRun run)
        {
            run.CarriedProduct?.Release();
            run.CarriedProduct = null;
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
                if (run.Animation != null)
                    run.Animation.SetPaused(stopped);
            }
        }

        private void OnDisable()
        {
            TrimPopulation(customers, 0);
            TrimPopulation(pedestrians, 0);
        }
    }
}
