using System;

namespace rp.spark
{
    internal static class TextUtil
    {
        public static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }
    }
}
