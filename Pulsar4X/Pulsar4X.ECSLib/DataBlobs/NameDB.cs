﻿using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;

namespace Pulsar4X.ECSLib
{
    [DebuggerDisplay("{" + nameof(DefaultName) + "}")]
    public class NameDB : BaseDataBlob
    {

        /// <summary>
        /// Each faction can have a different name for whatever entity has this blob.
        /// </summary>
        [JsonProperty]
        private readonly Dictionary<Entity, string> _names = new Dictionary<Entity, string>();

        [PublicAPI]
        public string DefaultName => _names[Entity.InvalidEntity];

        public NameDB() { }

        public NameDB(string defaultName)
        {
            _names.Add(Entity.InvalidEntity, defaultName);
        }

        #region Cloning Interface.

        public NameDB(NameDB nameDB)
        {
            _names = new Dictionary<Entity, string>(nameDB._names);
        }

        public override object Clone()
        {
            return new NameDB(this);
        }

        #endregion

        [PublicAPI]
        public string GetName(Entity requestingFaction)
        {
            string name;
            if (!_names.TryGetValue(requestingFaction, out name))
            {
                // Entry not found for the specific entity.
                // Return the default name.
                name = _names[Entity.InvalidEntity];
            }
            return name;
        }

        public string GetName(Entity requestingFaction, Game game, AuthenticationToken auth)
        {
     
            if (game.GetPlayerForToken(auth).AccessRoles[requestingFaction] < AccessRole.Intelligence)
                requestingFaction = Entity.InvalidEntity;
            return GetName(requestingFaction);
        }

        [PublicAPI]
        public void SetName(Entity requestingFaction, string specifiedName)
        {
            if (_names.ContainsKey(requestingFaction))
            {
                _names[requestingFaction] = specifiedName;
            }
            else
            {
                _names.Add(requestingFaction, specifiedName);
            }
        }
    }
}
