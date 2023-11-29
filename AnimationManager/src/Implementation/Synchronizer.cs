using AnimationManagerLib.API;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AnimationManagerLib
{
    public class Synchronizer : ISynchronizer
    {
        private EnumAppSide mSide;

        public void Init(ICoreAPI api, ISynchronizer.AnimationRunHandler runHandler, ISynchronizer.AnimationStopHandler stopHandler, string channelName)
        {
            mSide = api.Side;

            mRunHandler = runHandler;
            mStopHandler = stopHandler;

            if (api.Side == EnumAppSide.Server)
            {
                StartServerSide(api as ICoreServerAPI, channelName);
            }
            else if (api.Side == EnumAppSide.Client)
            {
                StartClientSide(api as ICoreClientAPI, channelName);
            }
        }
        public void Sync(AnimationRunPacket request)
        {
            if (mSide == EnumAppSide.Server)
            {
                BroadcastPacket(request);
            }
            else
            {
                SendPacket(request);
            }
        }
        public void Sync(AnimationStopPacket request)
        {
            if (mSide == EnumAppSide.Server)
            {
                BroadcastPacket(request);
            }
            else
            {
                SendPacket(request);
            }
        }


        // SERVER SIDE

        IServerNetworkChannel mServerNetworkChannel;
        private void StartServerSide(ICoreServerAPI api, string channelName)
        {
            mServerNetworkChannel = api.Network.RegisterChannel(channelName)
                                               .RegisterMessageType<AnimationRunPacket>()
                                               .SetMessageHandler<AnimationRunPacket>(OnServerPacket)
                                               .RegisterMessageType<AnimationStopPacket>()
                                               .SetMessageHandler<AnimationStopPacket>(OnServerPacket);
        }
        private void OnServerPacket<TPacket>(IServerPlayer fromPlayer, TPacket packet)
        {
            BroadcastPacket(packet, fromPlayer);
        }
        private void BroadcastPacket<TPacket>(TPacket packet, params IServerPlayer[] exceptFor)
        {
            mServerNetworkChannel.BroadcastPacket(packet, exceptFor);
        }

        // CLIENT SIDE

        IClientNetworkChannel mClientNetworkChannel;
        private ISynchronizer.AnimationRunHandler mRunHandler;
        private ISynchronizer.AnimationStopHandler mStopHandler;
        

        private void StartClientSide(ICoreClientAPI api, string channelName)
        {
            mClientNetworkChannel = api.Network.RegisterChannel(channelName)
                                               .RegisterMessageType<AnimationRunPacket>()
                                               .SetMessageHandler<AnimationRunPacket>(OnClientPacket)
                                               .RegisterMessageType<AnimationStopPacket>()
                                               .SetMessageHandler<AnimationStopPacket>(OnClientPacket);
        }
        private void SendPacket<TPacket>(TPacket packet)
        {
            mClientNetworkChannel.SendPacket(packet);
        }
        private void OnClientPacket(AnimationRunPacket packet)
        {
            mRunHandler(packet);
        }
        private void OnClientPacket(AnimationStopPacket packet)
        {
            mStopHandler(packet);
        }
    }
}
