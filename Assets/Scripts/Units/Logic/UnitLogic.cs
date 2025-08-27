using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Helpers;
using Units.Data;
using UnityEngine;
using UnityEngine.AI;
using Zenject;

namespace Units.Logic
{
	[RequireComponent(typeof(NavMeshAgent))]
	public abstract class UnitLogic : MonoBehaviour
	{
		public enum UnitState
		{
			Advance,
			MoveToPos,
			Attack,
			Dead
		}

		[Header("Data")] [SerializeField] protected UnitData unitData;
		[SerializeField, Range(1, 3)] protected int level = 1;
		[SerializeField] protected int teamId = 0;
		
		[Header("Cover")] [Tooltip("How far the unit will search for covers (world units).")]
		public float coverSearchRadius = 6f;

		[Tooltip("Distance to enemy at which the unit prefers to start seeking cover (fine-tune).")]
		public float coverSeekDistance = 4f;

		[Tooltip("Exclude covers that are further than this angle (degrees) behind the unit's forward. " +
		         "A larger angle is more permissive (keeps more side-covers).")]
		public float coverExcludeAngleDeg = 100f;

		[Header("Debug")] [Tooltip("Enable verbose logs for cover selection and nav failures.")]
		public bool debugCover = false;

		// interrupt tuning: how aggressively to interrupt moving-to-cover when enemy appears
		const float coverInterruptRatio = 0.7f; // 0..1, smaller => prefer cover more
		const float immediateInterruptDistance = 1.2f; // world units; very close enemy forces immediate interrupt

		protected UnitRuntimeStats stats;
		protected int currentHealth;
		protected NavMeshAgent agent;
		protected UnitState state = UnitState.Advance;
		protected Animator animator;

		protected Transform currentTarget; // enemy unit transform
		protected Transform enemyBase;
		protected Vector3 moveTargetPos; // used when MoveToPos state (nav target near cover)
		protected float lastAttackTime = -999f;
		protected bool isInitialized = false;

		// cover tracking
		protected Cover currentCover = null;
		protected Cover desiredCover = null; // store Cover component directly
		protected bool isInCover => currentCover != null;

		// store previous stopping distance when moving to cover
		float _prevStoppingDistance = -1f;

		// store when we started cover search (to detect if enemy fired during the process)
		float _coverSearchStartTime = -999f;

		protected static readonly int AnimIsWalking = Animator.StringToHash("isWalking");
		protected static readonly int AnimShoot = Animator.StringToHash("shoot");
		protected static readonly int AnimDie = Animator.StringToHash("die");

		[Inject] protected ITeamBaseProvider baseProvider;

		CancellationTokenSource _cts;

		// Expose last attack time so other units can detect if an enemy fired recently
		public float LastAttackTime => lastAttackTime;

		public virtual void Initialize(int team, int initLevel = 1, Transform explicitEnemyBase = null)
		{
			teamId = team;
			SetLevel(initLevel);

			if (explicitEnemyBase != null)
				enemyBase = explicitEnemyBase;
			else if (baseProvider != null)
				enemyBase = baseProvider.GetOpposingBaseTransform(teamId);

			isInitialized = true;
			state = UnitState.Advance;
		}

		protected virtual void Awake()
		{
			agent = GetComponent<NavMeshAgent>();
			animator = GetComponentInChildren<Animator>();

			if (animator == null)
				Debug.LogWarning($"{name}: Animator not found on children. Animations will be disabled.", this);

			if (unitData == null)
			{
				Debug.LogError($"{name}: UnitData not assigned.", this);
				enabled = false;
				return;
			}

			stats = unitData.GetStatsForLevel(level);
			currentHealth = stats.maxHealth;

			// IMPORTANT: runtime stopping distance equals shooting range
			agent.speed = stats.moveSpeed;
			agent.stoppingDistance = stats.attackRange;
			agent.angularSpeed = 120f;
			agent.acceleration = 8f;
			agent.updateRotation = true;
			agent.updatePosition = true;

			_cts = new CancellationTokenSource();
		}

		protected virtual void OnDestroy()
		{
			// Ensure occupied cover is released if unit is destroyed
			ReleaseCover();

			if (_cts != null)
			{
				_cts.Cancel();
				_cts.Dispose();
				_cts = null;
			}
		}

		protected virtual void Update()
		{
			if (!isInitialized) return;
			if (state == UnitState.Dead) return;

			agent.speed = stats.moveSpeed;

			// keep stopping distance in sync with shooting range only when we are NOT temporarily overriding it
			// _prevStoppingDistance >= 0 means we saved previous value and applied a temporary one (e.g. 0.2f)
			if (_prevStoppingDistance < 0f)
				agent.stoppingDistance = stats.attackRange;

			bool walking = (state == UnitState.Advance || state == UnitState.MoveToPos) &&
			               agent.velocity.sqrMagnitude > 0.01f;
			if (animator != null)
				animator.SetBool(AnimIsWalking, walking);

			TickStateMachine();
		}


		protected virtual void TickStateMachine()
		{
			// Now detection uses the same radius as attack/shooting range
			Transform enemyInRange = FindNearestEnemyInRange(stats.attackRange);
			Transform enemyInAttack = enemyInRange; // same zone: detection == attack-zone

			// debug: draw detection/attack
			if (enemyInRange != null)
				Debug.DrawLine(transform.position, enemyInRange.position, Color.yellow, 0.1f);

			// ---- interruption policy: only consider interrupting when we're moving to cover ----
			if (enemyInAttack != null && state == UnitState.MoveToPos)
			{
				var enemyLogic = enemyInAttack.GetComponentInParent<UnitLogic>();

				float distToEnemy = Vector3.Distance(transform.position, enemyInAttack.position);
				float distToCover = Vector3.Distance(transform.position, moveTargetPos);


				bool shouldInterrupt = false;

				// 1) desired cover lost -> interrupt
				if (desiredCover == null)
					shouldInterrupt = true;

				// 2) enemy started firing after we began searching for cover -> interrupt immediately
				else if (enemyLogic != null && enemyLogic.LastAttackTime > _coverSearchStartTime)
				{
					shouldInterrupt = true;
				}
				// 3) immediate proximity (very close) -> interrupt
				else if (distToEnemy <= immediateInterruptDistance)
				{
					shouldInterrupt = true;
				}

				if (shouldInterrupt)
				{
					if (debugCover) Debug.Log($"{name}: Interrupting cover move, switching to Attack.", this);

					// we were moving-to-cover (not yet occupying) -> just clear desiredCover and attack
					desiredCover = null;

					// restore stopping distance if it was changed for moving to cover
					if (_prevStoppingDistance >= 0f)
					{
						agent.stoppingDistance = _prevStoppingDistance;
						_prevStoppingDistance = -1f;
					}

					currentTarget = enemyInAttack;
					state = UnitState.Attack;
				}
			}


			// main state machine
			switch (state)
			{
				case UnitState.Advance:
					if (state == UnitState.Attack) break;

					if (enemyInRange != null)
					{
						float d = Vector3.Distance(transform.position, enemyInRange.position);

						// start seeking cover if enemy is within coverSeekDistance and is not yet firing
						if (d <= coverSeekDistance)
						{
							// Always try to find cover first when the enemy enters the attack zone.
							// We will only abandon cover search if the enemy starts firing AFTER we started searching,
							// or if cover is unavailable / enemy is extremely close (handled in interruption block).
							var candidate = FindNearestCoverAndPosition(transform.position, enemyInRange.position,
								coverSearchRadius);
							if (candidate.HasValue)
							{
								moveTargetPos = candidate.Value.position;
								desiredCover = candidate.Value.cover;

								// debug draw
								Debug.DrawLine(transform.position, moveTargetPos, Color.cyan, 2f);
								if (debugCover) Debug.Log($"{name}: Found cover candidate at {moveTargetPos}", this);

								// temporarily reduce stopping distance so unit actually goes to the nav point near the cover
								_prevStoppingDistance = agent.stoppingDistance;
								agent.stoppingDistance = 0.2f;

								// record when we started searching/moving to cover
								_coverSearchStartTime = Time.time;

								GoToPosition(moveTargetPos);
								state = UnitState.MoveToPos;
								currentTarget = enemyInRange;
								return;
							}
							else
							{
								// IMPORTANT FIX: previously we only faced the enemy and returned — that left the unit
								// in Advance state doing nothing. Now if no cover is found, we transition to Attack
								// so the unit will start shooting/chasing instead of getting stuck.
								if (debugCover) Debug.Log($"{name}: No cover found, switching to Attack.", this);
								currentTarget = enemyInRange;
								FaceTowards(enemyInRange.position);
								state = UnitState.Attack;
								return;
							}
						}

						// if enemy in attack zone but not within coverSeekDistance -> attack/chase
						if (d > coverSeekDistance)
						{
							currentTarget = enemyInRange;
							state = UnitState.Attack;
							return;
						}
					}

					AdvanceTowardsBase();
					break;

				case UnitState.MoveToPos:
					// arrival check: use both agent.remainingDistance and direct distance as fallback
					bool arrivedByNav = !agent.pathPending && agent.hasPath &&
					                    agent.remainingDistance <= Mathf.Max(0.25f, agent.stoppingDistance + 0.05f);
					bool arrivedByDistance = Vector3.Distance(transform.position, moveTargetPos) <= 0.35f;

					// additional fallback: if agent has no path but is very close to target
					bool arrivedByNoPathClose = (!agent.hasPath && !agent.pathPending &&
					                             Vector3.Distance(transform.position, moveTargetPos) <= 1.0f);

					if (arrivedByNav || arrivedByDistance || arrivedByNoPathClose)
					{
						// restore stopping distance
						if (_prevStoppingDistance >= 0f)
						{
							agent.stoppingDistance = _prevStoppingDistance;
							_prevStoppingDistance = -1f;
						}

						// Attempt to occupy the desired cover (race condition: another unit might have taken it)
						if (desiredCover != null)
						{
							var cov = desiredCover.GetComponent<Cover>() ?? desiredCover.GetComponentInParent<Cover>();
							if (cov != null)
							{
								// If the cover is already occupied by another unit, do not take it.
								// Fall back to attacking the target so we don't get stuck.
								if (cov.IsOccupied && cov.occupant != this)
								{
									if (debugCover) Debug.Log($"{name}: Arrived but cover is occupied by another unit. Abandoning cover.", this);
									desiredCover = null;
									currentCover = null;
									// fallback: start attacking (or you could search for another cover)
									if (currentTarget != null)
										state = UnitState.Attack;
									else
										state = UnitState.Advance;
								}
								else
								{
									// occupy the cover (marks it as ours)
									bool occupied = OccupyCover(cov);
									if (occupied)
									{
										if (debugCover) Debug.Log($"{name}: Successfully occupied cover at {cov.transform.position}", this);
										// start shooting from cover
										if (currentTarget != null)
											state = UnitState.Attack;
										else
											state = UnitState.Advance;
									}
									else
									{
										// Occupation failed for some reason: fallback to attack
										if (debugCover) Debug.Log($"{name}: Failed to occupy cover (race). Switching to Attack.", this);
										desiredCover = null;
										currentCover = null;
										if (currentTarget != null)
											state = UnitState.Attack;
										else
											state = UnitState.Advance;
									}
								}
							}
							else
							{
								desiredCover = null;
								state = currentTarget != null ? UnitState.Attack : UnitState.Advance;
							}
						}
						else
						{
							// no desired cover (maybe it was cleared) -> resume behavior
							if (currentTarget != null)
								state = UnitState.Attack;
							else
								state = UnitState.Advance;
						}

						// clear desiredCover now that we've processed arrival
						desiredCover = null;
					}

					break;

				case UnitState.Attack:
					if (currentTarget == null)
					{
						// release occupied cover when target disappears
						ReleaseCover();
						state = UnitState.Advance;
						return;
					}

					float distToTarget = Vector3.Distance(transform.position, currentTarget.position);

					// if target walked away -> chase (use attackRange as chase threshold)
					if (distToTarget > stats.attackRange * 1.05f)
					{
						// release cover before chasing
						ReleaseCover();
						GoToPosition(currentTarget.position);
						state = UnitState.MoveToPos;
						return;
					}

					agent.ResetPath();
					FaceTowards(currentTarget.position);
					TryAttack(currentTarget);
					break;
			}
		}

		#region Movement helpers

		protected void AdvanceTowardsBase()
		{
			if (enemyBase != null)
			{
				// release any occupied cover when advancing to base
				ReleaseCover();
				agent.SetDestination(enemyBase.position);
			}
		}

		protected void GoToPosition(Vector3 worldPos)
		{
			agent.SetDestination(worldPos);
		}

		protected void FaceTowards(Vector3 worldPos)
		{
			Vector3 dir = worldPos - transform.position;
			dir.y = 0f;
			if (dir.sqrMagnitude < 0.001f) return;
			Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
			transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
		}

		// result container
		protected struct CoverCandidateResult
		{
			public Vector3 position;
			public Cover cover;
		}

		/// <summary>
		/// Evaluate covers in radius and choose candidate that minimizes NAVMESH path length from the unit.
		/// Returns Position + corresponding Cover component.
		/// Fallback: if OverlapSphere finds no colliders, also try FindObjectsOfType<Cover>() so covers without colliders are still considered.
		/// Important: we exclude only those covers that are sufficiently *behind* the unit based on coverExcludeAngleDeg.
		/// Additionally we exclude covers that are already occupied by other units.
		/// </summary>
		protected CoverCandidateResult? FindNearestCoverAndPosition(Vector3 fromPosition, Vector3 enemyPosition,
			float searchRadius)
		{
			Collider[] hits = Physics.OverlapSphere(fromPosition, searchRadius);

			var candidates = new List<Cover>();

			// compute unit forward on XZ plane (fallbacks included)
			Vector3 forward = (agent != null ? agent.transform.forward : transform.forward);
			forward.y = 0f;
			if (forward.sqrMagnitude < 0.0001f)
			{
				forward = (enemyPosition - fromPosition);
				forward.y = 0f;
				if (forward.sqrMagnitude < 0.0001f)
					forward = Vector3.forward;
			}
			forward.Normalize();

			// compute the dot threshold from the specified exclude angle:
			// If dot < minDot -> the cover is "behind enough" to exclude.
			float minDot = Mathf.Cos(coverExcludeAngleDeg * Mathf.Deg2Rad);

			// helper local function: is cover behind the unit beyond configured threshold?
			bool IsBehindCover(Vector3 coverPos)
			{
				Vector3 toCover = coverPos - fromPosition;
				toCover.y = 0f;
				if (toCover.sqrMagnitude < 0.0001f)
					return false; // cover on top of us — not considered "behind"
				toCover.Normalize();
				float dot = Vector3.Dot(forward, toCover);
				// exclude only if dot < minDot (i.e. angle > coverExcludeAngleDeg)
				bool behind = dot < minDot;
				if (debugCover && behind)
					Debug.Log($"{name}: Excluding cover at {coverPos} (dot={dot:F2} < minDot={minDot:F2})", this);
				return behind;
			}

			if (hits != null && hits.Length > 0)
			{
				var seen = new HashSet<Cover>();
				foreach (var c in hits)
				{
					var cov = c.GetComponentInParent<Cover>();
					if (cov == null) continue;
					if (seen.Contains(cov)) continue;

					// exclude covers that are behind the unit (using the threshold)
					if (IsBehindCover(cov.transform.position)) continue;

					// exclude covers already occupied by other units
					if (cov.IsOccupied && cov.occupant != this)
					{
						if (debugCover) Debug.Log($"{name}: Skipping occupied cover at {cov.transform.position}", this);
						continue;
					}

					seen.Add(cov);
					candidates.Add(cov);
				}
			}

			// Fallback: if no covers found by OverlapSphere (maybe no colliders), search all Cover components in scene
			if (candidates.Count == 0)
			{
				var all = FindObjectsOfType<Cover>();
				foreach (var cov in all)
				{
					float d = Vector3.Distance(fromPosition, cov.transform.position);
					if (d <= searchRadius)
					{
						// exclude covers that are behind the unit
						if (IsBehindCover(cov.transform.position)) continue;

						// exclude covers already occupied by other units
						if (cov.IsOccupied && cov.occupant != this)
						{
							if (debugCover) Debug.Log($"{name}: Skipping occupied cover at {cov.transform.position}", this);
							continue;
						}

						candidates.Add(cov);
					}
				}
			}

			if (candidates.Count == 0)
				return null;

			Cover bestCover = null;
			Vector3 bestPos = Vector3.zero;
			float bestPathLen = float.MaxValue;

			foreach (var cov in candidates)
			{
				// compute position behind cover relative to enemy
				Vector3 dir = (cov.transform.position - enemyPosition);
				dir.y = 0f;
				if (dir.sqrMagnitude < 0.001f)
				{
					dir = (cov.transform.position - fromPosition);
					dir.y = 0f;
					if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
				}

				dir = dir.normalized;

				float behindDist = 0.6f + (agent != null ? agent.radius : 0.5f);
				Vector3 candidate = cov.transform.position + dir * behindDist;

				// sample NavMesh near candidate (bigger search radius for safety)
				NavMeshHit navHit;
				float sampleRange = 1.5f + (agent != null ? agent.radius : 0.5f);
				if (!NavMesh.SamplePosition(candidate, out navHit, sampleRange, NavMesh.AllAreas))
				{
					// try small offsets
					const int attempts = 8;
					bool found = false;
					for (int i = 0; i < attempts; i++)
					{
						float angle = i * Mathf.PI * 2f / attempts;
						Vector3 offset = Quaternion.Euler(0f, Mathf.Rad2Deg * angle, 0f) * (dir * behindDist);
						Vector3 tryPos = cov.transform.position + offset;
						if (NavMesh.SamplePosition(tryPos, out navHit, sampleRange, NavMesh.AllAreas))
						{
							found = true;
							break;
						}
					}

					if (!found)
						continue; // can't find navmesh near this cover
				}

				// compute nav path from current position to navHit.position
				NavMeshPath path = new NavMeshPath();
				bool pathFound = (agent != null)
					? agent.CalculatePath(navHit.position, path)
					: NavMesh.CalculatePath(fromPosition, navHit.position, NavMesh.AllAreas, path);
				if (!pathFound)
					continue;

				if (path.status != NavMeshPathStatus.PathComplete)
					continue;

				// calculate path length
				float pathLen = 0f;
				Vector3 prev = path.corners.Length > 0 ? path.corners[0] : fromPosition;
				for (int i = 1; i < path.corners.Length; i++)
				{
					pathLen += Vector3.Distance(prev, path.corners[i]);
					prev = path.corners[i];
				}

				if (pathLen < bestPathLen)
				{
					bestPathLen = pathLen;
					bestCover = cov;
					bestPos = navHit.position;
				}
			}

			if (bestCover == null)
				return null;

			if (debugCover) Debug.Log($"{name}: Selected cover at {bestPos} (pathLen={bestPathLen:F2})", this);

			return new CoverCandidateResult { position = bestPos, cover = bestCover };
		}


		#endregion

		#region Combat

		protected virtual void TryAttack(Transform target)
		{
			if (Time.time - lastAttackTime < stats.attackCooldown) return;
			lastAttackTime = Time.time;

			if (animator != null)
				animator.SetTrigger(AnimShoot);

			_ = PerformAttackAsync(target, _cts != null ? _cts.Token : CancellationToken.None);
		}

		protected virtual async UniTask PerformAttackAsync(Transform target, CancellationToken ct)
		{
			await UniTask.Yield(ct);

			if (target == null) return;

			var targetLogic = target.GetComponentInParent<UnitLogic>();
			float targetVul = 0f;
			if (targetLogic != null)
				targetVul = targetLogic.GetEffectiveVulnerability();

			float finalHitChance = stats.accuracy * (1f - Mathf.Clamp01(targetVul));
			float roll = UnityEngine.Random.value;
			if (roll <= finalHitChance)
			{
				if (targetLogic != null)
					targetLogic.ReceiveDamage(stats.damage);
			}
		}

		public float GetEffectiveVulnerability()
		{
			float v = stats.vulnerability;
			if (currentCover != null)
				v += currentCover.protection;
			return Mathf.Clamp01(v);
		}

		#endregion

		public virtual void ReceiveDamage(int amount)
		{
			if (state == UnitState.Dead) return;
			currentHealth -= amount;
			if (currentHealth <= 0) Die();
		}

		protected virtual void Die()
		{
			// release occupied cover before dying
			ReleaseCover();

			state = UnitState.Dead;
			if (agent != null) agent.isStopped = true;
			if (animator != null) animator.SetTrigger(AnimDie);
			if (_cts != null)
			{
				_cts.Cancel();
				_cts.Dispose();
				_cts = null;
			}

			Destroy(gameObject, 1f);
		}

		protected Transform FindNearestEnemyInRange(float radius)
		{
			Collider[] hits = Physics.OverlapSphere(transform.position, radius);
			Transform best = null;
			float bestDist = float.MaxValue;

			if (hits == null || hits.Length == 0) return null;

			foreach (var c in hits)
			{
				var u = c.GetComponentInParent<UnitLogic>();
				if (u == null) continue;
				if (u.gameObject == this.gameObject) continue;
				if (u.teamId == this.teamId) continue;

				// compute center-to-center distance and filter by radius
				float d = Vector3.Distance(transform.position, u.transform.position);
				if (d > radius) continue;

				if (d < bestDist)
				{
					bestDist = d;
					best = u.transform;
				}
			}

			return best;
		}

		public virtual void SetLevel(int newLevel)
		{
			level = Mathf.Clamp(newLevel, 1, 3);
			stats = unitData.GetStatsForLevel(level);
			currentHealth = stats.maxHealth;
			if (agent != null)
			{
				agent.speed = stats.moveSpeed;
				agent.stoppingDistance = stats.attackRange; // keep stopping distance synced with shooting range
			}
		}

		/// <summary>
		/// Try to occupy a cover. Returns true if occupation succeeded.
		/// This marks the Cover.occupant and stores currentCover.
		/// </summary>
		protected bool OccupyCover(Cover cov)
		{
			if (cov == null) return false;

			// if already occupied by ourselves, succeed
			if (cov.occupant == this)
			{
				currentCover = cov;
				return true;
			}

			// if occupied by another, fail
			if (cov.IsOccupied)
			{
				return false;
			}

			// occupy
			cov.occupant = this;
			currentCover = cov;
			return true;
		}

		/// <summary>
		/// Release current cover if we own it.
		/// Always call this instead of direct currentCover = null to keep occupancy consistent.
		/// </summary>
		protected void ReleaseCover()
		{
			if (currentCover != null)
			{
				if (currentCover.occupant == this)
					currentCover.occupant = null;
				currentCover = null;
			}
		}

		protected virtual void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(transform.position, stats.attackRange);

			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(transform.position, coverSearchRadius);

			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(transform.position, coverSeekDistance);

			if (enemyBase != null)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawLine(transform.position, enemyBase.position);
				Gizmos.DrawWireSphere(enemyBase.position, 0.5f);
			}

			// draw moveTargetPos if set
			if (moveTargetPos != Vector3.zero)
			{
				Gizmos.color = Color.cyan;
				Gizmos.DrawSphere(moveTargetPos, 0.12f);
			}
		}
	}
}
