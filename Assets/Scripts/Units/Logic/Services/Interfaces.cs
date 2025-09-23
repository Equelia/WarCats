// Services.Interfaces.cs  (обнови этот файл целиком)
using System.Threading;
using Cysharp.Threading.Tasks;
using Helpers;
using Units.Logic.Core;
using UnityEngine;

namespace Units.Logic.Services
{
    public interface IMovementService
    {
        void AdvanceTowardsBase(UnitContext ctx);
        void GoTo(UnitContext ctx, Vector3 pos);
        bool Arrived(UnitContext ctx, Vector3 pos);
        void ResetPath(UnitContext ctx);
        void SyncWalkingAnim(UnitContext ctx, bool isWalking);
        void OverrideStoppingDistance(UnitContext ctx, float tempValue);
        void RestoreStoppingDistance(UnitContext ctx);
    }

    public struct CoverCandidate
    {
        public Vector3 position;
        public Cover cover;
    }

    public interface ICoverService
    {
        /// <summary>
        /// Выбрать лучший вариант укрытия и вернуть точку на навмеш за ним.
        /// </summary>
        CoverCandidate? FindBest(UnitContext ctx, Vector3 fromPos, Vector3 enemyPos, float radius);

        /// <summary>Попытаться занять укрытие (ставит occupant и ctx.CurrentCover).</summary>
        bool Occupy(UnitContext ctx, Cover cov);

        /// <summary>Освободить текущее укрытие, если принадлежит этому юниту.</summary>
        void Release(UnitContext ctx);
    }

    public interface ISensorService
    {
        /// <summary>Найти ближайшего врага в радиусе.</summary>
        Transform FindNearestEnemy(UnitContext ctx, float radius);
    }

    public interface ICombatService
    {
        /// <summary>Попытаться атаковать текущую цель с учётом КД.</summary>
        void TryAttack(UnitContext ctx);

        /// <summary>Фактическая атака (шанс попадания, урон, FX-хуки).</summary>
        UniTask PerformAttackAsync(UnitContext ctx, Transform target, CancellationToken ct);
    }
}
