using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace MTypes
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MGroup : MGraphic
    {
        public const string classStr = "matlab.graphics.primitive.Group";
        
        public override void init()
        {
            base.init();
        }

        void Update()
        {
            base.Update();
        }

        public override void refresh()
        {
            base.refresh();
            if (needsRefresh)
                return;
        }
    }
}