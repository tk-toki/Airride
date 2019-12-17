﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaxSpeedItem : Item
{
    public override void CatchItem(Machine machine)
    {
        itemName = ItemName.MaxSpeed;
        base.CatchItem(machine);
        if (!limit)
        {
            machine.ChangeStatus(StatusType.MaxSpeed, mode);
            machine.ChangeStatus(StatusType.FlySpeed, mode);
            machine.ChangeStatus(StatusType.Brake, mode);
        }
    }
}