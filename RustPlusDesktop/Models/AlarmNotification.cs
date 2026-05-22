using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkDuckBot.Models;

    

    public record AlarmNotification(
        DateTime Timestamp,
        string Server,
        string DeviceName,
        uint? EntityId,
        string Message
    );

