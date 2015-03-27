﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

using Pulsar4X.ECSLib;

namespace WPFUI
{
    static class Program
    {
        public static void PulsarMain()
        {
                    
            Game game = new Game();
            Guid faction = game.EngineComms.Factions.Values.First(); //just get the ffirst one for now, till we've got ui to select.
            
            UI_Comms uicomms = new UI_Comms(game.EngineComms, faction);
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => uicomms.CheckEnginMessageQueue()));

            
        }
    }
}
