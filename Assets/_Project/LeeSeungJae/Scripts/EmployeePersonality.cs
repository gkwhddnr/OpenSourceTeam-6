using System;
using UnityEngine;

namespace EmployeeVoice
{
    // Keep the existing numeric order: these values are serialized in the integrated scene.
    public enum EmployeeAction
    {
        TAKE_ORDER, SERVE_FOOD, CLEAN_TABLE, TAKE_OUT_TRASH, RESTOCK,
        DROP_FOOD, WRONG_ORDER, THROW_AWAY_TABLE, KICK_CUSTOMER,
        IGNORE_COMMAND, COMPLAIN, REST,
        CLEAN, HANDLE_PAYMENT, CASHIER,
        ACCEPT_NORMAL, ACCEPT_COMPLAIN, MISTAKE, SLACK_OFF, REFUSE_EMOTION
    }

    public enum EmployeeArchetype { Standard, DiligentStandard, LazyComplainer, FragileTimid, LiteralRigid, PassionateHasty }

    [Serializable]
    public sealed class EmployeePersonality
    {
        public string employeeName = "Employee 1";
        public EmployeeArchetype archetype = EmployeeArchetype.Standard;
        [TextArea] public string traits = "성실하지만 가끔 지시를 잘못 이해한다.";
        [Range(0, 100)] public int diligence = 70;
        [Range(0, 100)] public int literalness = 20;
        [Range(0, 100)] public int laziness = 20;
        [Range(0, 100)] public int stress = 20;
        [Range(0, 100)] public int fatigue = 10;
        [Range(0, 100)] public int loyalty = 70;
    }
}
