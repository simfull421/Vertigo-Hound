// ============================================================================
// AnimationController.cs — 1인칭 애니메이션 전담 컴포넌트
// 사족보행 손 애니메이션, 이족보행, 벽 타기 시 암(Arm) 연출 등을 관리합니다.
// ============================================================================

using UnityEngine;

namespace VertigoHound.Player
{
    public class AnimationController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 참조
        // ──────────────────────────────────────────────

        [Header("=== 애니메이터 참조 ===")]
        [Tooltip("1인칭 팔(Arm) 모델의 Animator")]
        [SerializeField] private Animator armAnimator;

        // ──────────────────────────────────────────────
        // 애니메이터 파라미터 해시 (문자열 비교 방지)
        // ──────────────────────────────────────────────

        private static readonly int HashIsRunning = Animator.StringToHash("IsRunning");
        private static readonly int HashIsQuadRun = Animator.StringToHash("IsQuadRun");
        private static readonly int HashIsWallRun = Animator.StringToHash("IsWallRun");
        private static readonly int HashIsSliding = Animator.StringToHash("IsSliding");
        private static readonly int HashIsAirborne = Animator.StringToHash("IsAirborne");
        private static readonly int HashSpeed = Animator.StringToHash("Speed");

        // ──────────────────────────────────────────────
        // 상태별 애니메이션 전환 메서드
        // ──────────────────────────────────────────────

        /// <summary>이족보행 질주 애니메이션 재생</summary>
        public void PlayBipedalRun()
        {
            ResetAllBools();
            if (armAnimator != null)
                armAnimator.SetBool(HashIsRunning, true);
        }

        /// <summary>사족보행(네 발) 착지 애니메이션 재생</summary>
        public void PlayQuadRun()
        {
            ResetAllBools();
            if (armAnimator != null)
                armAnimator.SetBool(HashIsQuadRun, true);
        }

        /// <summary>벽 타기 애니메이션 재생</summary>
        public void PlayWallRun()
        {
            ResetAllBools();
            if (armAnimator != null)
                armAnimator.SetBool(HashIsWallRun, true);
        }

        /// <summary>슬라이딩 애니메이션 재생</summary>
        public void PlaySlide()
        {
            ResetAllBools();
            if (armAnimator != null)
                armAnimator.SetBool(HashIsSliding, true);
        }

        /// <summary>공중 체공 애니메이션 재생</summary>
        public void PlayAirborne()
        {
            ResetAllBools();
            if (armAnimator != null)
                armAnimator.SetBool(HashIsAirborne, true);
        }

        /// <summary>
        /// 현재 속도를 애니메이터에 전달합니다.
        /// 블렌드 트리에서 속도에 따른 애니메이션 블렌딩에 사용합니다.
        /// </summary>
        /// <param name="speed">현재 이동 속도</param>
        public void SetSpeed(float speed)
        {
            if (armAnimator != null)
                armAnimator.SetFloat(HashSpeed, speed);
        }

        /// <summary>모든 Bool 파라미터를 false로 초기화합니다</summary>
        private void ResetAllBools()
        {
            if (armAnimator == null) return;
            armAnimator.SetBool(HashIsRunning, false);
            armAnimator.SetBool(HashIsQuadRun, false);
            armAnimator.SetBool(HashIsWallRun, false);
            armAnimator.SetBool(HashIsSliding, false);
            armAnimator.SetBool(HashIsAirborne, false);
        }
    }
}
