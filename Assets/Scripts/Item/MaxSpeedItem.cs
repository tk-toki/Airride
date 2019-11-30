﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaxSpeedItem : Item
{
    public override void CatchItem(Machine machine)
    {
        statusName = StatusName.MaxSpeed;
        base.CatchItem(machine);
    }
}