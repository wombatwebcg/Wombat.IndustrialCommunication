using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Wombat.IndustrialCommunication
{
    internal static class SnapshotFilePathHelper
    {
        internal static string Build(string serverType, string endpointIdentity, string channelId = null)
        {
            var normalizedEndpointIdentity = Sanitize(endpointIdentity);
            if (!string.IsNullOrWhiteSpace(channelId))
            {
                var normalizedChannelId = Sanitize(channelId);
                return Path.Combine(AppContext.BaseDirectory, "Snapshots", $"{serverType}_{normalizedChannelId}_{normalizedEndpointIdentity}.snapshot");
            }

            return Path.Combine(AppContext.BaseDirectory, "Snapshots", $"{serverType}_{normalizedEndpointIdentity}.snapshot");
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "default";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                builder.Append(invalidChars.Contains(character) ? '_' : character);
            }

            return builder.ToString();
        }
    }
}
