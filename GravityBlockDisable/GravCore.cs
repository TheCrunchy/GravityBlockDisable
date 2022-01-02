using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Torch.Managers;
using Torch.API.Managers;
using Torch.Session;
using Torch.API.Session;
using Torch.Managers.PatchManager;
using System.Reflection;
using Sandbox.Game.Entities.Cube;
using System.IO;
using Sandbox.Game.GameSystems;

namespace GravityBlockDisable
{
    public class GravCore : TorchPluginBase
    {
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            var sessionManager = Torch.Managers.GetManager<TorchSessionManager>();

            if (sessionManager != null)
            {
                sessionManager.SessionStateChanged += SessionChanged;
            }

             SetupConfig();

        }
        private void SetupConfig()
        {
            FileUtils utils = new FileUtils();
         
            if (File.Exists(StoragePath + "\\GravityDisableConfig.xml"))
            {
                config = utils.ReadFromXmlFile<Config>(StoragePath + "\\GravityDisableConfig.xml");
                utils.WriteToXmlFile<Config>(StoragePath + "\\GravityDisableConfig.xml", config, false);
            }
            else
            {
               config = new Config();
                config.BlockPairNamesToDisableOutOfGrav.Add("LargeRefinery");
                utils.WriteToXmlFile<Config>(StoragePath + "\\GravityDisableConfig.xml", config, false);
            }

        }
        private void SessionChanged(ITorchSession session, TorchSessionState newState)
        {

        }
        public static Config config;
        [PatchShim]
        public class FunctionalBlockPatch
        {

            internal static readonly MethodInfo update =
            typeof(MyFunctionalBlock).GetMethod("UpdateBeforeSimulation10", BindingFlags.Instance | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

            internal static readonly MethodInfo update2 =
            typeof(MyFunctionalBlock).GetMethod("UpdateBeforeSimulation100", BindingFlags.Instance | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

            internal static readonly MethodInfo updatePatch =
           typeof(FunctionalBlockPatch).GetMethod(nameof(KeepDisabled), BindingFlags.Static | BindingFlags.Public) ??
           throw new Exception("Failed to find patch method");
            public static void Patch(PatchContext ctx)
            {
                ctx.GetPattern(update).Prefixes.Add(updatePatch);
                ctx.GetPattern(update2).Prefixes.Add(updatePatch);
            }

            public static Dictionary<long, BlockState> DisabledBlocks = new Dictionary<long, BlockState>();
            public class BlockState
            {
                public Boolean InGrav = false;
                public DateTime NextCheck = DateTime.Now;  
            }

            public static Boolean DoUpdate(long entityId, BlockState state, MyFunctionalBlock __instance)
            {
                //recheck if in gravity
                if (MyGravityProviderSystem.IsPositionInNaturalGravity(__instance.PositionComp.GetPosition()))
                {
                    //it is in gravity, so now we just update its next update time
                    state.InGrav = true;
                    state.NextCheck = DateTime.Now.AddSeconds(config.SecondsBetweenGravChecks);

                    //Lazy way to do this, java hashmaps are better
                    DisabledBlocks.Remove(entityId);
                    DisabledBlocks.Add(entityId, state);
                    return true;

                }
                else {
                    //its not in gravity, disable it
                    state.InGrav = false;
                    state.NextCheck = DateTime.Now.AddSeconds(config.SecondsBetweenGravChecks);

                    //Lazy way to do this, java hashmaps are better
                    DisabledBlocks.Remove(entityId);
                    DisabledBlocks.Add(entityId, state);
                  
                    return false;
                }
            }
            public static Boolean KeepDisabled(MyFunctionalBlock __instance)
            {

                if (DisabledBlocks.TryGetValue(__instance.EntityId, out BlockState state))
                {
                    if (DateTime.Now >= state.NextCheck)
                    {
                        if (DoUpdate(__instance.EntityId, state, __instance))
                        {
                            return true;
                        }
                        else  {
                            __instance.Enabled = false;
                            return false;
                        }
                    }
                    else
                    {
                        if (!state.InGrav)
                        {
                            __instance.Enabled = false;
                            return false;
                        }
                    }
                }
                if (config.BlockPairNamesToDisableOutOfGrav.Contains(__instance.BlockDefinition.BlockPairName))
                {
                    //we arent keeping track of it, but its configured to, so we should see if its in gravity and track it
                    BlockState state2 = new BlockState();
                    if (DoUpdate(__instance.EntityId, state2, __instance))
                    {
                        return true;
                    }
                    else
                    {
                        __instance.Enabled = false;
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
