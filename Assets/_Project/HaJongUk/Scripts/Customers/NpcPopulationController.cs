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
        private enum ShoppingStyle { Direct, Deliberating, Indecisive, AskWhenMissing, LeaveWhenMissing, HesitateAndLeave, AskForLocation }
        private enum Step { Enter, Consider, Browse, Queue, Checkout, Sit, Exit, Outside, WalkSidewalk }

        private static readonly Step[] CustomerSequence =
        {
            Step.Enter, Step.Consider, Step.Browse, Step.Queue, Step.Checkout, Step.Sit, Step.Exit, Step.Outside
        };

        [Header("손님 좌석")]
        [SerializeField] private Transform customerSeat;
        [SerializeField] private Transform customerSeatApproach;
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
            public bool Sitting;
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
            public Vector3 QuestionTargetPosition;
            public Vector3 QuestionApproach;
            public float QuestionTravelSeconds;
            public bool QuestionAsked;
            public bool QuestionAnsweredUnavailable;
            public bool DemoQuestion;
            public float QuestionSeconds;
            public float FollowDirectionsAt;
            public Transform AnswerLocation;
            public ProductOption Product;
            public NpcCarriedProduct CarriedProduct;

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
        [SerializeField] private bool requireRosterCharacter;
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
        [SerializeField] private Transform[] queueSlots = Array.Empty<Transform>();
        [SerializeField] private Transform exit;
        [Header("상품 위치 질문 대상 (선택)")]
        [SerializeField] private Transform employeeQuestionTarget;
        [SerializeField] private Transform playerQuestionTarget;
        [SerializeField, Min(1f)] private float questionTimeout = 15f;
        [SerializeField] private string demoMissingProductName;
        [SerializeField] private bool showcasePurchaseAndQuestion;
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
        private int showcaseCustomers;
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
        public int CompletedSeatVisits { get; private set; }
        public int CompletedProductQuestions { get; private set; }
        public int SeatedCustomerId => seatedCustomer != null && seatedCustomer.Sitting ? seatedCustomer.Id : -1;
        public int RouteFailures { get; private set; }
        public bool IsPaused { get; private set; }
        public float CustomerSpeed => customerSpeed;
        public float PedestrianSpeed => pedestrianSpeed;
        public event Action<int, string, Transform> CustomerAskedForProduct;

        public bool TryAnswerProductQuestion(int customerId, Transform productLocation)
        {
            AgentRun customer = FindPendingQuestion(customerId);
            if (customer == null || customer.DemoQuestion || productLocation == null) return false;
            customer.AnswerLocation = productLocation;
            CompletedProductQuestions++;
            return true;
        }

        public bool IsProductQuestionPending(int customerId) => FindPendingQuestion(customerId) != null;

        public bool TryResolveProductQuestion(int customerId, Transform respondent, out Transform pickupLocation)
        {
            pickupLocation = null;
            AgentRun customer = FindPendingQuestion(customerId);
            if (IsPaused || customer == null || respondent == null || customer.QuestionTarget != respondent ||
                (respondent.position - customer.Agent.transform.position).sqrMagnitude > 3f * 3f)
                return false;

            if (!customer.DemoQuestion)
            {
                ProductOption desired = products[customer.DesiredProductIndex];
                if (IsProductInStock(desired))
                    pickupLocation = desired.pickupLocation != null ? desired.pickupLocation : shelf;
            }
            customer.AnswerLocation = pickupLocation;
            customer.QuestionAnsweredUnavailable = pickupLocation == null;
            if (pickupLocation != null && respondent == employeeQuestionTarget)
                customer.FollowDirectionsAt = customer.QuestionSeconds + 0.9f;
            CompletedProductQuestions++;
            return true;
        }

        private AgentRun FindPendingQuestion(int customerId)
        {
            foreach (AgentRun customer in customers)
                if (customer.Id == customerId && customer.Agent != null &&
                    customer.StepIndex < customer.Sequence.Length && customer.Sequence[customer.StepIndex] == Step.Consider &&
                    customer.QuestionAsked && !customer.QuestionAnsweredUnavailable && customer.AnswerLocation == null)
                    return customer;
            return null;
        }

        // 선반 재고를 조회만 한다. 실제 재고 차감·매출 반영은 외부 시스템의 책임이다.
        public Func<string, bool> CanTakeFromShelf { get; set; }
        public event Action<int, string> ProductPickedUp;
        public bool WaitForExternalCheckout { get => waitForExternalCheckout; set => waitForExternalCheckout = value; }

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
                (requireRosterCharacter && (characterRoster == null || characterRoster.Count == 0)) ||
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
            {
                var fallback = instance.GetComponentInChildren<NpcCharacterAnimation>();
                var visual = characterRoster.ApplyRandom(instance.transform, fallback);
                if (requireRosterCharacter && (visual == null || visual == fallback))
                {
                    Dispose(agent);
                    RouteFailures++;
                    return;
                }
            }
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
            if (showcasePurchaseAndQuestion && showcaseCustomers < 3)
            {
                customer.ShoppingStyle = showcaseCustomers++ switch
                {
                    0 => ShoppingStyle.Direct,
                    1 => ShoppingStyle.AskForLocation,
                    _ => ShoppingStyle.AskWhenMissing
                };
                customer.DemoQuestion = customer.ShoppingStyle == ShoppingStyle.AskWhenMissing &&
                    !string.IsNullOrWhiteSpace(demoMissingProductName);
                return;
            }

            // Shuffle each group so all behaviors appear even in short test runs.
            if (shoppingStyles == null || shoppingStyles.IsComplete)
                shoppingStyles = CreateSearchOrder((int)ShoppingStyle.AskForLocation + 1);
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
            run.StepIndex++;
            return run.StepIndex == run.Sequence.Length ? Result.Success : Result.Running;
        }

        private static void UpdateAnimation(AgentRun run, Step step, Result result)
        {
            var presentation = run.Animation;
            if (presentation == null)
                return;

            if (result == Result.Running)
            {
                if (step == Step.Sit && run.Sitting)
                    return;
                if (step == Step.Browse && run.BrowseSeconds > 0f)
                    return; // The pickup step updates the reach target and progress.
                if (step == Step.Consider && run.SeekingHelp && run.QuestionAsked)
                {
                    presentation.Show(NpcAnimationState.Ask);
                    return;
                }
            }

            Vector3 velocity = run.Agent.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            if (speed > 0.08f)
                presentation.Show(run.IsRunner ? NpcAnimationState.Run : NpcAnimationState.Walk);
            else
                presentation.Show(run.Role == Role.Customer ? NpcAnimationState.Wait : NpcAnimationState.Idle);
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
                    return SitAfterCheckout(run, elapsed);
                case Step.Exit:
                    return run.Navigation.MoveTo(run.Agent, exit, elapsed);
                case Step.Outside:
                case Step.WalkSidewalk:
                    return run.Navigation.MoveTo(run.Agent, run.SidewalkDestination, elapsed);
                default:
                    return Result.Failure;
            }
        }

        private Result SitAfterCheckout(AgentRun run, float elapsed)
        {
            if (!run.Sitting)
            {
                if ((seatedCustomer != null && seatedCustomer != run) || customerSeat == null || customerSeatedSeconds <= 0f ||
                    run.Animation == null || !run.Animation.CanShow(NpcAnimationState.SitDown) ||
                    !run.Animation.CanShow(NpcAnimationState.Seated))
                    return Result.Success;

                seatedCustomer = run;
                Transform approach = customerSeatApproach != null ? customerSeatApproach : customerSeat;
                Result arrival = run.Navigation.MoveTo(run.Agent, approach, elapsed);
                if (arrival == Result.Running) return Result.Running;
                if (arrival == Result.Failure)
                {
                    seatedCustomer = null;
                    return Result.Success;
                }
                run.Agent.ResetPath();
                run.Agent.isStopped = true;
                run.Agent.updatePosition = false;
                run.Agent.updateRotation = false;
                run.Agent.transform.SetPositionAndRotation(customerSeat.position, customerSeat.rotation);
                run.Sitting = true;
            }

            run.SitSeconds += elapsed;
            float sitDownSeconds = NpcCharacterAnimation.SitDownSeconds;
            if (run.SitSeconds >= sitDownSeconds + customerSeatedSeconds)
            {
                if (!run.Agent.isOnNavMesh) return Result.Failure;
                run.Agent.transform.position = run.Agent.nextPosition;
                run.Agent.updatePosition = true;
                run.Agent.updateRotation = true;
                run.Agent.isStopped = false;
                run.Sitting = false;
                seatedCustomer = null;
                CompletedSeatVisits++;
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
                        return Result.Running;
                    if (!customer.Agent.isOnNavMesh)
                        return Result.Failure;
                    customer.Agent.isStopped = false;
                    return LeaveWithoutPurchase(customer);
                case ShoppingStyle.Deliberating:
                    return PauseAndChooseProduct(customer, elapsed);
                case ShoppingStyle.Indecisive:
                    return ConsiderMultipleProducts(customer, elapsed);
                case ShoppingStyle.AskForLocation:
                    int firstProduct = UnityEngine.Random.Range(0, products.Length);
                    customer.DesiredProductIndex = -1;
                    for (int offset = 0; offset < products.Length; offset++)
                    {
                        int index = (firstProduct + offset) % products.Length;
                        if (!IsProductInStock(products[index]) || customers.Exists(other => other.ProductIndex == index))
                            continue;
                        customer.DesiredProductIndex = index;
                        break;
                    }
                    if (customer.DesiredProductIndex < 0)
                        customer.DesiredProductIndex = ChooseSearchTarget();
                    customer.SeekingHelp = true;
                    return AskForProduct(customer, elapsed);
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
                return Result.Running;

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
                return Result.Running;

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
                    return Result.Running;

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
            return customer.ConsiderSeconds < customer.RequiredConsiderSeconds
                ? Result.Running : ChooseDisplayedProduct(customer);
        }

        private Result SearchShelves(AgentRun customer, float elapsed)
        {
            if (customer.SearchOrder == null)
            {
                customer.SearchOrder = CreateSearchOrder(products.Length);
                customer.DesiredProductIndex = customer.DemoQuestion ? -1 : ChooseSearchTarget();
            }

            if ((customer.DemoQuestion && customer.SearchOrder.VisitedCount >= 2) ||
                !customer.SearchOrder.TryGetCurrent(out int index))
                return HandleMissingProduct(customer);

            ProductOption option = products[index];
            Transform location = option.pickupLocation != null ? option.pickupLocation : shelf;
            Result movement = customer.Navigation.MoveTo(customer.Agent, location, elapsed);
            if (movement == Result.Running)
                return Result.Running;

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
                    return Result.Running;
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
            if (!customer.QuestionAsked)
            {
                customer.QuestionTravelSeconds += elapsed;
                if (customer.QuestionTravelSeconds >= questionTimeout || !TryFindQuestionApproach(customer))
                    return LeaveWithoutPurchase(customer);
                Result movement = customer.Navigation.MoveTo(customer.Agent, customer.QuestionApproach, elapsed);
                if (movement == Result.Failure) return LeaveWithoutPurchase(customer);
                if (movement != Result.Success) return Result.Running;
                customer.QuestionAsked = true;
                string name = customer.DemoQuestion ? demoMissingProductName : products[customer.DesiredProductIndex].productName;
                CustomerAskedForProduct?.Invoke(customer.Id, name,
                    customer.QuestionTarget);
            }
            float minimumQuestionTime = customer.Animation != null ? Mathf.Min(questionTimeout, QuestionAnimationSeconds) : 0f;
            if (customer.Animation != null && customer.QuestionTarget != null)
                FaceInteraction(customer.Agent, customer.QuestionTarget.position, elapsed);
            if (customer.QuestionSeconds >= Mathf.Max(minimumQuestionTime, customer.FollowDirectionsAt) &&
                customer.AnswerLocation != null && IsProductInStock(products[customer.DesiredProductIndex]) &&
                TryReserveSpecificProduct(customer, customer.DesiredProductIndex))
            {
                ProductOption product = customer.Product;
                product.pickupLocation = customer.AnswerLocation;
                customer.Product = product;
                return Result.Success;
            }
            if (customer.QuestionAnsweredUnavailable && customer.QuestionSeconds >= minimumQuestionTime)
                return LeaveWithoutPurchase(customer);
            customer.QuestionSeconds += elapsed;
            if (customer.QuestionSeconds < questionTimeout) return Result.Running;
            return LeaveWithoutPurchase(customer);
        }

        private bool TryFindQuestionApproach(AgentRun customer)
        {
            Transform target = customer.QuestionTarget;
            if (target != null && target.gameObject.activeInHierarchy &&
                (target.position - customer.QuestionTargetPosition).sqrMagnitude < 0.6f * 0.6f)
                return true;

            bool playerCloser = playerQuestionTarget != null && (employeeQuestionTarget == null ||
                (playerQuestionTarget.position - customer.Agent.transform.position).sqrMagnitude <
                (employeeQuestionTarget.position - customer.Agent.transform.position).sqrMagnitude);
            Transform first = playerCloser ? playerQuestionTarget : employeeQuestionTarget;
            Transform second = playerCloser ? employeeQuestionTarget : playerQuestionTarget;
            return TrySetQuestionApproach(customer, first) || TrySetQuestionApproach(customer, second);
        }

        private static bool TrySetQuestionApproach(AgentRun customer, Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy ||
                !customer.Navigation.TryFindApproach(customer.Agent, target.position, out Vector3 approach))
                return false;

            customer.QuestionTarget = target;
            customer.QuestionTargetPosition = target.position;
            customer.QuestionApproach = approach;
            return true;
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
            GameObject shelfItem = customer.Product.shelfItem;
            bool pointBeforePickup = customer.Animation != null && shelfItem != null &&
                customer.Product.category == ProductCategory.Cigarette;
            customer.BrowseSeconds += elapsed;
            if (customer.Animation != null)
            {
                Vector3? target = shelfItem != null ? (Vector3?)shelfItem.transform.position : null;
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
                if ((customer.ShoppingStyle == ShoppingStyle.AskWhenMissing ||
                    customer.ShoppingStyle == ShoppingStyle.AskForLocation) && !customer.QuestionAsked)
                {
                    customer.SeekingHelp = true;
                    customer.StepIndex = Array.IndexOf(customer.Sequence, Step.Consider) - 1;
                    return Result.Success;
                }
                return LeaveWithoutPurchase(customer);
            }

            GameObject visual = customer.Product.visualPrefab != null ? customer.Product.visualPrefab : productPrefab;
            customer.CarriedProduct = NpcCarriedProduct.Attach(customer.Product.shelfItem, visual,
                customer.Agent.transform, customer.Animation != null ? customer.Animation.RightHand : null);

            ProductPickedUp?.Invoke(customer.Id, GetProductTag(customer.Product.category));
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
            ReleaseCarriedProduct(customer);
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
                    run.Agent.isStopped = stopped || run.Sitting;
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
