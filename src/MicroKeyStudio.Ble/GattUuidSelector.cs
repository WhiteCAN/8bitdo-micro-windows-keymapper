using MicroKeyStudio.Protocol.Packets;

namespace MicroKeyStudio.Ble;

public static class GattUuidSelector
{
    public static Guid? SelectMicroServiceUuid(IEnumerable<Guid> availableServiceUuids)
    {
        foreach (Guid uuid in availableServiceUuids)
        {
            if (uuid == MicroProtocolConstants.ServiceUuid)
            {
                return uuid;
            }
        }

        return null;
    }
}
