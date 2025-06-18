using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;


#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
using UnityEngine.InputSystem;
#endif

namespace ForsakenLegacy
{
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class PlayerController : MonoBehaviour, IDataPersistence
    {
        [Header("Player")]
        [SerializeField] private bool canMove = true;
        public bool CanMove
        {
            get { return canMove; }
            set { canMove = value; }
        }
        private float _moveSpeed = 2.0f;
        public bool _isInAbility = false;
        private bool _movementPressed;
        public float _maxWalkingAngle = 60f;
        private float _speed;
        private Vector3 _velocity;
        private bool _isStopping;

        // Stamina System
        [Header("Stamina System")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaRegenRate = 10f;
        [SerializeField] private float staminaDrainRate = 20f;
        private float currentStamina;
        [SerializeField] private Image staminaBar;

        // Footsteps and Gravity
        public float MaxSlopeAngle = 60f;
        private readonly float _minSlopeAngle = 0.001f;
        private readonly float _groundDist = 0.1f;
        public LayerMask GroundLayer;

        [Header("Audio")]
        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        private AudioSource _audioSource;
        [Range(0, 1)] public float FootstepAudioVolume = 0.2f;

        // Camera
        [Header("Camera")]
        public Transform MainCamera;

        // Player Readonly Variables
        private readonly float _walkSpeed = 2.0f;
        private readonly float _sprintSpeed = 7f;
        private readonly float _acceleration = 3f;
        private readonly float _maxWalkSpeed = 0.5f;
        private readonly float _maxSprintSpeed = 2.0f;
        private readonly float _rotateSpeed = 90f;
        private readonly int _maxBounces = 5;
        private readonly float _anglePower = 0.5f;

        // Player Components
        private PlayerInput _playerInput;
        private Animator _animator;
        private Rigidbody _rb;
        private InputController _input;
        private CapsuleCollider _capsuleCollider;

        // Animations Hash
        private readonly int _speedHash = Animator.StringToHash("Speed");
        private readonly int _fallHash = Animator.StringToHash("Fall");
        private readonly int _isStoppingHash = Animator.StringToHash("isStopping");

        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        public void LoadData(GameData data)
        {
            this.transform.position = data.playerPosition;
        }

        public void SaveData(ref GameData data)
        {
            data.playerPosition = this.transform.position;
        }

        private void Start()
        {
            _hasAnimator = TryGetComponent(out _animator);

            _input = GetComponent<InputController>();
            _rb = GetComponent<Rigidbody>();
            _playerInput = GetComponent<PlayerInput>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _audioSource = GetComponent<AudioSource>();

            _rb.isKinematic = true;

            currentStamina = maxStamina; // Initialize current stamina to max stamina
        }

        private void FixedUpdate()
        {
            bool isAttacking = gameObject.GetComponent<AttackMelee>().isAttacking;
            _hasAnimator = TryGetComponent(out _animator);

            if (_animator.GetCurrentAnimatorStateInfo(0).IsName("Hit")) { canMove = false; }
            else { canMove = true; }

            if (canMove && !_isInAbility && !isAttacking) { MoveInput(); }

            PushOutIfPenetrating();
        }

        private void PushOutIfPenetrating()
        {
            // //Check if the player is too close to the ground and adjust position upwards if necessary
            // if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 1, GroundLayer))
            // {
            //     transform.position += Vector3.up * (1 - hit.distance);
            //     Debug.Log("pushing out");
            // }
        }

        private void MoveInput()
        {
            // Get movement input from InputController
            Vector2 moveInput = _input.move;
            bool sprint = _input.sprint;

            // Update stamina
            sprint = UpdateStamina(sprint);

            bool falling = !IsGrounded(out RaycastHit groundHit);
            if (falling)
            {
                _velocity += Physics.gravity * Time.deltaTime;
            }
            else
            {
                _velocity = Vector3.zero;
            }

            Vector3 moveDirection = Vector3.forward * moveInput.y + Vector3.right * moveInput.x;
            moveDirection.y = 0f;
            moveDirection.Normalize();

            Vector3 movement = moveDirection * _moveSpeed * Time.deltaTime;

            if (!falling)
            {
                movement = Vector3.ProjectOnPlane(movement, groundHit.normal);
            }

            transform.position = MovePlayer(movement);
            transform.position = MovePlayer(_velocity * Time.deltaTime);

            float currentMaxSpeed = sprint ? _maxSprintSpeed : _maxWalkSpeed;

            _moveSpeed = sprint ? _sprintSpeed : _walkSpeed;

            if (moveDirection.magnitude >= 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                float rotationSpeed = 5f;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            ChangeSpeedAnimation(moveInput, currentMaxSpeed);
            DetectSuddenStop();

            if (_hasAnimator)
            {
                _animator.SetFloat(_speedHash, _speed);
                _animator.SetBool(_fallHash, false);
            }
        }

        private bool UpdateStamina(bool sprint)
        {
            if (sprint && currentStamina > 0)
            {
                currentStamina -= staminaDrainRate * Time.deltaTime;
                if (currentStamina < 0)
                {
                    currentStamina = 0;
                }
            }
            else if (sprint && currentStamina <= 0)
            {
                sprint = false; // Disable sprint if out of stamina
            }
            else
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                if (currentStamina > maxStamina)
                {
                    currentStamina = maxStamina;
                }
            }

            // Update stamina bar UI
            if (staminaBar != null)
            {
                staminaBar.fillAmount = currentStamina / maxStamina;
            }
            return sprint;
        }

        private void ChangeSpeedAnimation(Vector2 moveInput, float currentMaxSpeed)
        {
            float targetSpeed;

            if (moveInput == Vector2.zero)
            {
                targetSpeed = 0f;
                if (_movementPressed)
                {
                    _movementPressed = false;
                    _animator.SetTrigger(_isStoppingHash);
                }
            }
            else
            {
                targetSpeed = currentMaxSpeed;
                _movementPressed = true;
                _animator.ResetTrigger(_isStoppingHash);
            }

            _speed = Mathf.MoveTowards(_speed, targetSpeed, _acceleration * Time.deltaTime);
        }

        private void DetectSuddenStop()
        {
            float previousSpeed = _speed;
            bool wasRunning = previousSpeed > 1.5f;
            bool isNowIdle = Mathf.Approximately(_speed, 0f);

            if (wasRunning && isNowIdle)
            {
                _isStopping = true;
            }
            else
            {
                _isStopping = false;
            }
        }

        public Vector3 MovePlayer(Vector3 movement)
        {
            Vector3 position = transform.position;
            Vector3 remaining = movement;
            int bounces = 0;

            while (bounces < _maxBounces && remaining.magnitude > _minSlopeAngle)
            {
                float distance = remaining.magnitude;
                if (!CastSelf(position, transform.rotation, remaining.normalized, distance, out RaycastHit hit))
                {
                    position += remaining;
                    break;
                }

                if (hit.distance == 0)
                {
                    break;
                }

                float fraction = hit.distance / distance;
                position += remaining * (fraction);
                position += hit.normal * _minSlopeAngle * 2;
                remaining *= (1 - fraction);

                Vector3 planeNormal = hit.normal;

                float angleBetween = Vector3.Angle(hit.normal, remaining) - 90.0f;
                angleBetween = Mathf.Min(MaxSlopeAngle, Mathf.Abs(angleBetween));
                float normalizedAngle = angleBetween / MaxSlopeAngle;

                remaining *= Mathf.Pow(1 - normalizedAngle, _anglePower) * 0.9f + 0.1f;

                Vector3 projected = Vector3.ProjectOnPlane(remaining, planeNormal).normalized * remaining.magnitude;

                if (projected.magnitude + _minSlopeAngle < remaining.magnitude)
                {
                    remaining = Vector3.ProjectOnPlane(remaining, Vector3.up).normalized * remaining.magnitude;
                }
                else
                {
                    remaining = projected;
                }

                bounces++;
            }

            return position;
        }

        public bool CastSelf(Vector3 pos, Quaternion rot, Vector3 dir, float dist, out RaycastHit hit)
        {
            Vector3 center = rot * _capsuleCollider.center + pos;
            float radius = _capsuleCollider.radius;
            float height = _capsuleCollider.height;

            Vector3 bottom = center + rot * Vector3.down * (height / 2 - radius);
            Vector3 top = center + rot * Vector3.up * (height / 2 - radius);

            IEnumerable<RaycastHit> hits = Physics.CapsuleCastAll(top, bottom, radius, dir, dist, ~0, QueryTriggerInteraction.Ignore).Where(hit => hit.collider.transform != transform);
            bool didHit = hits.Count() > 0;

            float closestDist = didHit ? Enumerable.Min(hits.Select(hit => hit.distance)) : 0;
            IEnumerable<RaycastHit> closestHit = hits.Where(hit => hit.distance == closestDist);

            hit = closestHit.FirstOrDefault();

            return didHit;
        }

        private bool IsGrounded(out RaycastHit groundHit)
        {
            bool onGround = CastSelf(transform.position, transform.rotation, Vector3.down, _groundDist, out groundHit);
            float angle = Vector3.Angle(groundHit.normal, Vector3.up);
            return onGround && angle < _maxWalkingAngle;
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    _audioSource.clip = FootstepAudioClips[index];
                    _audioSource.Play();
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.transform.position, FootstepAudioVolume);
            }
        }
    }
}