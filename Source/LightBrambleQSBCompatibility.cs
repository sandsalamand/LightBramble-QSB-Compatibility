using Mirror;
using OWML.Common;
using OWML.ModHelper;
using OWML.ModHelper.Events;
using QSB.Messaging;
using QSB.Player;
using System;
using System.Collections;
using UnityEngine;
using static LightBramble.LightBramble;

namespace LightBrambleQSBCompatibility
{
	public class LightBrambleQSBCompatibility : ModBehaviour
	{
		public static LightBrambleQSBCompatibility inst;

		LightBramble.LightBramble lbBehaviour;

		private void Awake()
		{
			if (inst == null)
				inst = this;
			else
				Destroy(this);
		}

		private void Start()
		{
			lbBehaviour = (LightBramble.LightBramble)ModHelper.Interaction.TryGetMod("Alek.LightBramble");
			if (lbBehaviour == null)
			{
				ModHelper.Console.WriteLine("Light Bramble is not installed, so LBQSBCompatibility will not work! Disabling...", MessageType.Fatal);
				Destroy(gameObject);
				return;
			}
			ModHelper.Console.WriteLine($"{nameof(LightBrambleQSBCompatibility)} is loaded!", MessageType.Success);

			lbBehaviour.ConfigChanged += LBClientConfigChanged;
			QSBPlayerManager.OnAddPlayer += JoinedGame;
		}

		private void JoinedGame(PlayerInfo playerInfo)
		{
			//if we're the host and it's not us joining, then send a message to the players to sync them to host's config
			if (QSB.QSBCore.IsHost && !playerInfo.IsLocalPlayer)
			{
				ModHelper.Console.WriteLine("pushing LBConfigMessage because player joined game");
				PushConfigMessage(lbBehaviour.currentConfig);
			}
		}

		private void LBClientConfigChanged(BrambleConfig pushConfig)
		{
			ModHelper.Console.WriteLine("pushing LBConfigMessage because config changed");
			PushConfigMessage(pushConfig);
		}

		//this is called by the overriden OnReceiveRemote in LightBrambleConfigMessage
		//when the client receives a config message from another player, set the client's LightBramble settings to the message data
		public void ReceivedLBConfigMessage(BrambleConfig incomingConfig)
		{
			lbBehaviour.currentConfig = incomingConfig;
			ModHelper.Console.WriteLine("received LBConfigMessage");
		}

		//sends a message containing BrambleConfig data to all players
		private void PushConfigMessage(BrambleConfig config)
		{
			ModHelper.Console.WriteLine("pushing LBConfigMessage");
			LightBrambleConfigMessage configMessage = new LightBrambleConfigMessage(config);
			QSBMessageManager.Send(configMessage);
		}
	}

	public class LightBrambleConfigMessage : QSBMessage
	{
		protected BrambleConfig config;

		public LightBrambleConfigMessage(BrambleConfig brambleConfig)
		{
			config = brambleConfig;
		}

		public override void OnReceiveRemote()
		{
			LightBrambleQSBCompatibility.inst.ReceivedLBConfigMessage(config);
		}

		public override void Serialize(NetworkWriter writer)
		{
			base.Serialize(writer);

			BrambleConfigFlags configFlags = (BrambleConfigFlags.SwapMusic | BrambleConfigFlags.DisableFog);

			configFlags = configFlags |
				(config.swapMusic ? BrambleConfigFlags.SwapMusic : 0) |
				(config.disableFish ? BrambleConfigFlags.DisableFish : 0) |
				(config.disableFog ? BrambleConfigFlags.DisableFog : 0)
			;

			byte finalConfigByte = (byte)configFlags;
			writer.Write(finalConfigByte);
		}

		public override void Deserialize(NetworkReader reader)
		{
			base.Deserialize(reader);
			byte configByte = reader.Read<byte>();
			BrambleConfigFlags configFlags = (BrambleConfigFlags)configByte;

			config.swapMusic = configFlags.HasFlag(BrambleConfigFlags.SwapMusic);
			config.disableFish = configFlags.HasFlag(BrambleConfigFlags.DisableFish);
			config.disableFog = configFlags.HasFlag(BrambleConfigFlags.DisableFog);
		}
	}

	[Flags]
	public enum BrambleConfigFlags
	{
		None = 0,
		SwapMusic = 1,
		DisableFish= 2,
		DisableFog = 4
	}

	public static class PlayerInfoExtensions
	{
		public static void ExecuteWhenReady(this PlayerInfo playerInfo, MonoBehaviour runner, Action action)
		{
			runner.StartCoroutine(ExecuteWhenPlayerInfoReadyCoroutine(playerInfo, action));
		}

		private static IEnumerator ExecuteWhenPlayerInfoReadyCoroutine(PlayerInfo playerInfo, Action action)
		{
			while (!playerInfo.IsReady)
			{
				yield return new WaitForEndOfFrame();
			}
			action?.Invoke();
		}
	}
}