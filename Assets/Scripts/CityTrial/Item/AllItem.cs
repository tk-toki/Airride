﻿using System;

public class AllItem : Item
{
    public override void CatchItem(Machine machine)
    {
        itemName = ItemName.All;
        base.CatchItem(machine);
        if (!limit)
        {
            foreach(StatusType type in Enum.GetValues(typeof(StatusType)))
            {
                machine.ChangeStatus(type, mode);
            }
        }
    }
}