using System;
using System.Collections.Generic;
using System.Text;
using Wombat.IndustrialCommunication.Abstractions;

namespace Wombat.IndustrialCommunication.Messaging
{
   public class DeviceMessageFactory
    {
        public static T CreateDeviceMessage<T>(byte[] frame)
    where T : IDeviceMessage, new()
        {
            //Create the message
            T message = new T();

            //initialize it
            message.Initialize(frame);

            //return it
            return message;
        }

    }
}
