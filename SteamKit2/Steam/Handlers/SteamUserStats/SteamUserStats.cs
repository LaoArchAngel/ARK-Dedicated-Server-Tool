﻿/*
 * This file is subject to the terms and conditions defined in
 * file 'license.txt', which is part of this source code package.
 */



using SteamKit2.Internal;

namespace SteamKit2
{
    /// <summary>
    /// This handler handles Steam user statistic related actions.
    /// </summary>
    public sealed partial class SteamUserStats : ClientMsgHandler
    {

        /// <summary>
        /// Retrieves the number of current players or a given <see cref="GameID"/>.
        /// Results are returned in a <see cref="NumberOfPlayersCallback"/>.
        /// </summary>
        /// <param name="gameId">The GameID to request the number of players for.</param>
        /// <returns>The Job ID of the request. This can be used to find the appropriate <see cref="NumberOfPlayersCallback"/>.</returns>
        public JobID GetNumberOfCurrentPlayers( GameID gameId )
        {
            var msg = new ClientMsg<MsgClientGetNumberOfCurrentPlayers>();
            msg.SourceJobID = Client.GetNextJobID();

            msg.Body.GameID = gameId;

            Client.Send( msg );

            return msg.SourceJobID;
        }


        /// <summary>
        /// Handles a client message. This should not be called directly.
        /// </summary>
        /// <param name="packetMsg">The <see cref="SteamKit2.IPacketMsg"/> instance containing the event data.</param>
        public override void HandleMsg( IPacketMsg packetMsg )
        {
            switch ( packetMsg.MsgType )
            {
                case EMsg.ClientGetNumberOfCurrentPlayersResponse:
                    HandleNumberOfPlayersResponse( packetMsg );
                    break;
            }
        }


        #region ClientMsg Handlers
        void HandleNumberOfPlayersResponse( IPacketMsg packetMsg )
        {
            var msg = new ClientMsg<MsgClientGetNumberOfCurrentPlayersResponse>( packetMsg );

            var callback = new NumberOfPlayersCallback( msg.Header.TargetJobID, msg.Body );
            Client.PostCallback( callback );
        }
        #endregion
    }
}
