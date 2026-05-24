using UnityEngine;

public class AnimatorMovementTest : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Movement Test")]
    [SerializeField] private bool sprint;
    [SerializeField] private bool walk;
    [SerializeField] private bool isGrounded = true;

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsArmedHash = Animator.StringToHash("IsArmed");
    private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");

    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int TurnLeftHash = Animator.StringToHash("TurnLeft");
    private static readonly int TurnRightHash = Animator.StringToHash("TurnRight");
    private static readonly int TalkHash = Animator.StringToHash("Talk");

    private static readonly int IdleIndexHash = Animator.StringToHash("IdleIndex");
    private static readonly int WeaponTypeHash = Animator.StringToHash("WeaponType");

    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int ReloadHash = Animator.StringToHash("Reload");
    private static readonly int ThrowGrenadeHash = Animator.StringToHash("ThrowGrenade");

    private static readonly int DamageHash = Animator.StringToHash("Damage");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int DamageIndexHash = Animator.StringToHash("DamageIndex");
    private static readonly int DeathIndexHash = Animator.StringToHash("DeathIndex");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    private static readonly int IsEmotingHash = Animator.StringToHash("IsEmoting");
    private static readonly int DanceIndexHash = Animator.StringToHash("DanceIndex");
    private static readonly int DanceHash = Animator.StringToHash("Dance");
    private static readonly int StopEmoteHash = Animator.StringToHash("StopEmote");

    private Vector2 smoothMove;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (animator == null)
            return;

        UpdateMovement();
        UpdateBaseLayerTest();
        UpdateWeaponLayerTest();
        UpdateDamageDeathTest();
        UpdateEmoteTest();
    }

    private void UpdateMovement()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector2 input = new Vector2(x, y);

        float multiplier = 0.7f; // Run

        if (walk)
            multiplier = 0.35f;

        if (sprint && y > 0f)
            multiplier = 1f;

        Vector2 targetMove = input * multiplier;

        smoothMove = Vector2.Lerp(
            smoothMove,
            targetMove,
            Time.deltaTime * 10f
        );

        animator.SetFloat(MoveXHash, smoothMove.x);
        animator.SetFloat(MoveYHash, smoothMove.y);
        animator.SetFloat(SpeedHash, smoothMove.magnitude);
        animator.SetBool(IsGroundedHash, isGrounded);
    }

    private void UpdateEmoteTest()
    {
        if (animator.GetBool(IsDeadHash))
            return;

        if (Input.GetKeyDown(KeyCode.F1))
            PlayDance(1);

        if (Input.GetKeyDown(KeyCode.F2))
            PlayDance(2);

        if (Input.GetKeyDown(KeyCode.F3))
            PlayDance(3);

        if (Input.GetKeyDown(KeyCode.F4))
            PlayDance(4);

        if (Input.GetKeyDown(KeyCode.F5))
            PlayDance(5);

        if (Input.GetKeyDown(KeyCode.F6))
            PlayDance(6);

        if (Input.GetKeyDown(KeyCode.F7))
            PlayDance(7);

        if (Input.GetKeyDown(KeyCode.F8))
            PlayDance(8);

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            animator.SetBool(IsEmotingHash, false);
            animator.SetTrigger(StopEmoteHash);
        }
    }

    private void PlayDance(int danceIndex)
    {
        if (animator.GetBool(IsArmedHash))
            return;

        if (animator.GetFloat(SpeedHash) > 0.05f)
            return;

        if (!isGrounded)
            return;

        animator.SetBool(IsEmotingHash, true);
        animator.SetInteger(DanceIndexHash, danceIndex);
        animator.SetTrigger(DanceHash);
    }

    private void UpdateBaseLayerTest()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            animator.SetTrigger(JumpHash);
            isGrounded = false;
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            isGrounded = true;
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            animator.SetTrigger(TurnLeftHash);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            animator.SetTrigger(TurnRightHash);
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            animator.SetTrigger(TalkHash);
        }

        // Z Ч обычный Idle01
        if (Input.GetKeyDown(KeyCode.Z))
        {
            animator.SetInteger(IdleIndexHash, 0);
        }

        // X Ч переход в Idle02
        if (Input.GetKeyDown(KeyCode.X))
        {
            animator.SetInteger(IdleIndexHash, 1);
        }
    }

    private void UpdateWeaponLayerTest()
    {
        // 0 Ч убрать оружие
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            SetWeapon(0);
        }

        // 1 Ч пистолет
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetWeapon(1);
        }

        // 2 Ч два пистолета
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetWeapon(2);
        }

        // 3 Ч винтовка
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetWeapon(3);
        }

        // 4 Ч автомат
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SetWeapon(4);
        }

        // 5 Ч базука
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            SetWeapon(5);
        }

        if (Input.GetMouseButtonDown(1))
        {
            bool isArmed = animator.GetBool(IsArmedHash);

            if (isArmed)
                animator.SetBool(IsAimingHash, true);
        }

        if (Input.GetMouseButtonUp(1))
        {
            animator.SetBool(IsAimingHash, false);
        }
        // Ћ ћ Ч выстрел
        if (Input.GetMouseButtonDown(0))
        {
            if (animator.GetBool(IsArmedHash))
                animator.SetTrigger(ShootHash);
        }

        // R Ч перезар€дка
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (animator.GetBool(IsArmedHash))
                animator.SetTrigger(ReloadHash);
        }

        // F Ч бросить гранату
        if (Input.GetKeyDown(KeyCode.F))
        {
            animator.SetTrigger(ThrowGrenadeHash);
        }
    }

    private void SetWeapon(int weaponType)
    {
        bool isArmed = weaponType != 0;

        animator.SetInteger(WeaponTypeHash, weaponType);
        animator.SetBool(IsArmedHash, isArmed);

        if (!isArmed)
            animator.SetBool(IsAimingHash, false);
    }

    private void UpdateDamageDeathTest()
    {
        // H Ч получить урон
        if (Input.GetKeyDown(KeyCode.H))
        {
            if (animator.GetBool(IsDeadHash))
                return;

            int damageIndex = Random.Range(0, 2);

            animator.SetInteger(DamageIndexHash, damageIndex);
            animator.SetTrigger(DamageHash);
        }

        // K Ч умереть
        if (Input.GetKeyDown(KeyCode.K))
        {
            if (animator.GetBool(IsDeadHash))
                return;

            int deathIndex = Random.Range(0, 3);

            animator.SetInteger(DeathIndexHash, deathIndex);
            animator.SetBool(IsDeadHash, true);
            animator.SetBool(IsAimingHash, false);
            animator.SetBool(IsArmedHash, false);
            animator.SetInteger(WeaponTypeHash, 0);

            animator.SetTrigger(DeathHash);
        }

        // L Ч тестовый reset смерти
        if (Input.GetKeyDown(KeyCode.L))
        {
            animator.SetBool(IsDeadHash, false);
            animator.SetBool(IsGroundedHash, true);
            isGrounded = true;

            animator.Play("Locomotion", 0, 0f);
        }
    }
}