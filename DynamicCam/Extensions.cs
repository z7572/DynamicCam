// QOL-Ex
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynamicCam;

public static class Extensions
{
    public static bool IsAI(this Controller controller)
    {
        return Plugin.IsQOLExLoaded
            ? controller.isAI && !controller.gameObject.GetComponent("QOL.AFKManager")
            : controller.isAI;
    }
}
