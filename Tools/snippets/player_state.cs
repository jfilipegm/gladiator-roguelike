// Dumps player/motor/camera state during Play mode.
var p = Gladiator.Gameplay.Player.Main;
if (p == null) return "no player";
var m = p.GetComponent<Gladiator.Gameplay.PlayerMotor>();
var cam = UnityEngine.Camera.main;
var t = m.Telemetry;
var target = p.transform.Find("CameraTarget");
bool camInside = UnityEngine.Physics.CheckSphere(cam.transform.position, 0.08f, Gladiator.Gameplay.Layers.EnvironmentMask, UnityEngine.QueryTriggerInteraction.Ignore);
bool los = !UnityEngine.Physics.Linecast(cam.transform.position, target.position, Gladiator.Gameplay.Layers.EnvironmentMask, UnityEngine.QueryTriggerInteraction.Ignore);
return "pos=" + p.transform.position.ToString("F2") + " state=" + m.CurrentState + " hv=" + m.HorizontalSpeed.ToString("F2") + " vy=" + m.Velocity.y.ToString("F2")
  + " | cam=" + cam.transform.position.ToString("F2") + " camYaw=" + cam.transform.eulerAngles.y.ToString("F0") + " refYaw=" + p.ReferenceYaw.ToString("F0") + " fov=" + cam.fieldOfView.ToString("F1")
  + " camDist=" + UnityEngine.Vector3.Distance(cam.transform.position, target.position).ToString("F2") + " camInsideGeo=" + camInside + " lineOfSight=" + los
  + " | telemetry topSpeed=" + t.TopHorizontalSpeed.ToString("F2") + " topSlide=" + t.TopSlideSpeed.ToString("F2") + " lastJumpH=" + t.LastJumpHeight.ToString("F2") + " lastSlideBoost=" + t.LastSlideBoost.ToString("F2") + " jumps=" + t.Jumps + " slides=" + t.Slides + " lands=" + t.Lands
  + " | bot=" + p.IsBotControlled + " timeScale=" + UnityEngine.Time.timeScale;
