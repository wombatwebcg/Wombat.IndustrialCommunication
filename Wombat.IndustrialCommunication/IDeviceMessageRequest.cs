using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication
{
   public interface IDeviceMessageRequest:IDeviceMessage
    {
        void ValidateResponse(IDeviceMessage response);

    }
}
