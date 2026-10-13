using UnityEngine;
using UnityEngine.Playables;

namespace WildWasteland.Modules.PlayerBody
{
    internal sealed partial class Body
    {
        private float _fpJumpLogNext;
        private string _fpJumpLogState;
        internal bool TryJumpHead(out Vector3 position)
        {
            position = Vector3.zero;
            Transform head;
            if (_inCar || Traversal != null || _prone > .01f || _actionW <= .001f || _actionClip == null || !_actionClip.Contains("Jump") || !Bones.TryGetValue("mixamorig:Head", out head)) return false;
            position = head.position; return true;
        }

        public void FitFirstPersonJump(bool firstPerson, float dt)
        {
            if (!PlayerBodyModule.FirstPersonJumpFix.Value || Traversal != null || _inCar || _prone > .01f)
                return;
            if (Core.Log.Verbose && Game.Player != null && Game.PlayerCamera != null)
            {
                string state = Game.JumpState;
                bool recovery = _actionW > .001f && _actionClip != null && _actionClip.Contains("Jump");
                if ((state != "Idle" || state != _fpJumpLogState || recovery) && Time.unscaledTime >= _fpJumpLogNext)
                {
                    Transform head;
                    if (Bones.TryGetValue("mixamorig:Head", out head))
                        PlayerBodyModule.Verbose("Jump alignment: " + state + ", move=" + Game.MoveState + ", clip=" + _actionClip + ", weight=" + _actionW.ToString("0.00") + ", playerY=" + Game.Player.transform.position.y.ToString("0.000") + ", eyeY=" + Game.PlayerCamera.position.y.ToString("0.000") + ", headY=" + head.position.y.ToString("0.000") + ", height=" + Game.StandingHeight.ToString("0.000"));
                    Transform left, right;
                    if (Bones.TryGetValue("mixamorig:LeftFoot", out left) && Bones.TryGetValue("mixamorig:RightFoot", out right))
                        PlayerBodyModule.Verbose("Jump feet: " + state + ", firstPerson=" + firstPerson + ", clipTime=" + (_action.IsValid() ? _action.GetTime().ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) : "none") + ", groundY=" + (Game.Player.transform.position.y - Game.StandingHeight * .5f).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + ", leftY=" + left.position.y.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + ", rightY=" + right.position.y.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                    _fpJumpLogState = state; _fpJumpLogNext = Time.unscaledTime + 0.1f;
                }
            }
        }
    }
}
