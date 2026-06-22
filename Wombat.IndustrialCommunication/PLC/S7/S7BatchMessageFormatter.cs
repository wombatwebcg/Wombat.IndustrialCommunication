namespace Wombat.IndustrialCommunication.PLC
{
    internal static class S7BatchMessageFormatter
    {
        public static string BuildReadDispatchMessage(S7BatchReadDispatchAnalysis decision, string detail)
        {
            var prefix = decision == null
                ? null
                : $"BatchReadPath={decision.Mode}; Reason={decision.DecisionReason}";

            if (string.IsNullOrWhiteSpace(detail))
            {
                return prefix;
            }

            return string.IsNullOrEmpty(prefix) ? detail : prefix + "; " + detail;
        }

        public static string BuildWriteDispatchMessage(string path, string detail)
        {
            var prefix = $"BatchWritePath={path}";
            if (string.IsNullOrWhiteSpace(detail))
            {
                return prefix;
            }

            return prefix + "; " + detail;
        }
    }
}
