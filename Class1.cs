﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTTest
{
    class MQTTSetup
    {
        public static string BrokerAddress { get; internal set; }
        public static int BrokerPort { get; internal set; }
        public static bool isEncrypted { get; internal set; }
        public static string ClientId { get; internal set; }
        public static string Username { get; internal set; }
        public static string Password { get; internal set; }
        public static string Topic { get; internal set; }
    }
}
