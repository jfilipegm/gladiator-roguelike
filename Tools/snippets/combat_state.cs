var em = Gladiator.Gameplay.EnemyManager.Instance;
var p = Gladiator.Gameplay.Player.Main;
var ws = p.GetComponent<Gladiator.Gameplay.WeaponSystem>();
var hp = p.GetComponent<Gladiator.Gameplay.PlayerHealth>();
return "alive=" + em.AliveCount + " high=" + em.HighWater + " swings=" + ws.Swings + " hits=" + ws.Hits + " playerHp=" + hp.Hp.ToString("F0") + " deaths=" + hp.Deaths + " dmgNumbers=" + Gladiator.Core.DevCounters.DamageNumbers + " pos=" + p.transform.position.ToString("F1") + " fps~" + (1f / UnityEngine.Time.smoothDeltaTime).ToString("F0");
