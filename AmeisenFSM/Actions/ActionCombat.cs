﻿using AmeisenCoreUtils;
using AmeisenData;
using AmeisenFSM.Interfaces;
using AmeisenUtilities;
using static AmeisenFSM.Objects.Delegates;

namespace AmeisenFSM.Actions
{
    internal class ActionCombat : IAction
    {
        private Me Me
        {
            get { return AmeisenDataHolder.Instance.Me; }
            set { AmeisenDataHolder.Instance.Me = value; }
        }

        private Unit Target
        {
            get { return AmeisenDataHolder.Instance.Target; }
            set { AmeisenDataHolder.Instance.Target = value; }
        }

        public Start StartAction { get { return Start; } }
        public DoThings StartDoThings { get { return DoThings; } }
        public Exit StartExit { get { return Stop; } }

        public void Start() { }

        public void DoThings() { }

        public void Stop() { }

        private void FaceTarget()
        {
            if (Target != null)
            {
                Target.Update();
                AmeisenCore.InteractWithGUID(
                    Target.pos,
                    Target.Guid,
                    InteractionType.FACETARGET
                    );
            }
        }

        private void CastSpellByName(string name, bool onMyself)
        {
            AmeisenCore.CastSpellByName(name, onMyself);
        }

        private SpellInfo GetSpellInfo(string name, bool onMyself)
        {
           return AmeisenCore.GetSpellInfo(name);
        }
    }
}