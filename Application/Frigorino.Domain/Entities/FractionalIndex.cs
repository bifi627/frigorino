namespace Frigorino.Domain.Entities
{
    // Canonical fractional-indexing algorithm (Figma/rocicorp "Implementing Fractional Indexing").
    // Mints opaque lexicographic string keys with unbounded precision: a key can always be
    // generated strictly between any two distinct keys, so ordering never exhausts and a reorder
    // is always a single-row write. Keys are compared with ordinal (byte) string comparison —
    // the DB column MUST use the C collation to match (see ListItemConfiguration).
    //
    // Replaces the old integer SortOrderCalculator. Pure: no DbContext, no I/O.
    public static class FractionalIndex
    {
        private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        // Smallest/largest integer heads bound the integer-part length encoding.
        private const string SmallestInteger = "A00000000000000000000000000";

        public static string GenerateKeyBetween(string? a, string? b)
        {
            if (a is not null)
            {
                ValidateOrderKey(a);
            }
            if (b is not null)
            {
                ValidateOrderKey(b);
            }
            if (a is not null && b is not null && string.CompareOrdinal(a, b) >= 0)
            {
                throw new ArgumentException($"Order key '{a}' is not less than '{b}'.");
            }

            if (a is null)
            {
                if (b is null)
                {
                    return "a" + Digits[0]; // "a0"
                }
                var ib0 = GetIntegerPart(b);
                var fb0 = b[ib0.Length..];
                if (ib0 == SmallestInteger)
                {
                    return ib0 + Midpoint("", fb0);
                }
                if (string.CompareOrdinal(ib0, b) < 0)
                {
                    return ib0;
                }
                var dec = DecrementInteger(ib0)
                    ?? throw new InvalidOperationException("Cannot generate key before the smallest possible key.");
                return dec;
            }

            if (b is null)
            {
                var ia0 = GetIntegerPart(a);
                var fa0 = a[ia0.Length..];
                var inc = IncrementInteger(ia0);
                return inc is null ? ia0 + Midpoint(fa0, null) : inc;
            }

            var ia = GetIntegerPart(a);
            var fa = a[ia.Length..];
            var ib = GetIntegerPart(b);
            var fb = b[ib.Length..];
            if (ia == ib)
            {
                return ia + Midpoint(fa, fb);
            }
            var i = IncrementInteger(ia)
                ?? throw new InvalidOperationException("Cannot increment integer part.");
            return string.CompareOrdinal(i, b) < 0 ? i : ia + Midpoint(fa, null);
        }

        // n evenly distributed keys strictly between a and b (used by the backfill seed).
        public static IReadOnlyList<string> GenerateKeysBetween(string? a, string? b, int n)
        {
            if (n < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }
            if (n == 0)
            {
                return Array.Empty<string>();
            }
            if (n == 1)
            {
                return new[] { GenerateKeyBetween(a, b) };
            }
            if (b is null)
            {
                var c = GenerateKeyBetween(a, null);
                var resultUp = new List<string> { c };
                for (var x = 0; x < n - 1; x++)
                {
                    c = GenerateKeyBetween(c, null);
                    resultUp.Add(c);
                }
                return resultUp;
            }
            if (a is null)
            {
                var c = GenerateKeyBetween(null, b);
                var resultDown = new List<string> { c };
                for (var x = 0; x < n - 1; x++)
                {
                    c = GenerateKeyBetween(null, c);
                    resultDown.Add(c);
                }
                resultDown.Reverse();
                return resultDown;
            }
            var mid = n / 2;
            var midKey = GenerateKeyBetween(a, b);
            var result = new List<string>();
            result.AddRange(GenerateKeysBetween(a, midKey, mid));
            result.Add(midKey);
            result.AddRange(GenerateKeysBetween(midKey, b, n - mid - 1));
            return result;
        }

        private static string Midpoint(string a, string? b)
        {
            if (b is not null && string.CompareOrdinal(a, b) >= 0)
            {
                throw new ArgumentException($"Midpoint: '{a}' >= '{b}'.");
            }
            if (a.Length > 0 && a[^1] == '0' || (b is not null && b.Length > 0 && b[^1] == '0'))
            {
                throw new ArgumentException("Trailing zero in fractional part.");
            }
            if (b is not null)
            {
                var n = 0;
                while ((n < a.Length ? a[n] : '0') == b[n])
                {
                    n++;
                }
                if (n > 0)
                {
                    return b[..n] + Midpoint(n < a.Length ? a[n..] : "", b[n..]);
                }
            }
            var digitA = a.Length > 0 ? Digits.IndexOf(a[0]) : 0;
            var digitB = b is not null ? Digits.IndexOf(b[0]) : Digits.Length;
            if (digitB - digitA > 1)
            {
                var midDigit = (int)Math.Round(0.5 * (digitA + digitB), MidpointRounding.AwayFromZero);
                return Digits[midDigit].ToString();
            }
            if (b is not null && b.Length > 1)
            {
                return b[..1];
            }
            return Digits[digitA] + Midpoint(a.Length > 0 ? a[1..] : "", null);
        }

        private static int GetIntegerLength(char head)
        {
            if (head is >= 'a' and <= 'z')
            {
                return head - 'a' + 2;
            }
            if (head is >= 'A' and <= 'Z')
            {
                return 'Z' - head + 2;
            }
            throw new ArgumentException($"Invalid integer head '{head}'.");
        }

        private static string GetIntegerPart(string key)
        {
            var len = GetIntegerLength(key[0]);
            if (len > key.Length)
            {
                throw new ArgumentException($"Invalid order key '{key}'.");
            }
            return key[..len];
        }

        private static void ValidateOrderKey(string key)
        {
            if (key == SmallestInteger)
            {
                throw new ArgumentException($"Invalid order key '{key}'.");
            }
            var i = GetIntegerPart(key);
            var f = key[i.Length..];
            if (f.Length > 0 && f[^1] == '0')
            {
                throw new ArgumentException($"Invalid order key '{key}' (trailing zero).");
            }
        }

        private static string? IncrementInteger(string x)
        {
            var head = x[0];
            var digs = x[1..].Select(c => Digits.IndexOf(c)).ToList();
            var carry = true;
            for (var i = digs.Count - 1; carry && i >= 0; i--)
            {
                var d = digs[i] + 1;
                if (d == Digits.Length)
                {
                    digs[i] = 0;
                }
                else
                {
                    digs[i] = d;
                    carry = false;
                }
            }
            if (carry)
            {
                if (head == 'Z')
                {
                    return "a" + Digits[0];
                }
                if (head == 'z')
                {
                    return null;
                }
                var h = (char)(head + 1);
                if (h > 'a')
                {
                    digs.Add(0);
                }
                else
                {
                    digs.RemoveAt(digs.Count - 1);
                }
                return h + new string(digs.Select(d => Digits[d]).ToArray());
            }
            return head + new string(digs.Select(d => Digits[d]).ToArray());
        }

        private static string? DecrementInteger(string x)
        {
            var head = x[0];
            var digs = x[1..].Select(c => Digits.IndexOf(c)).ToList();
            var borrow = true;
            for (var i = digs.Count - 1; borrow && i >= 0; i--)
            {
                var d = digs[i] - 1;
                if (d == -1)
                {
                    digs[i] = Digits.Length - 1;
                }
                else
                {
                    digs[i] = d;
                    borrow = false;
                }
            }
            if (borrow)
            {
                if (head == 'a')
                {
                    return "Z" + Digits[^1];
                }
                if (head == 'A')
                {
                    return null;
                }
                var h = (char)(head - 1);
                if (h < 'Z')
                {
                    digs.Add(Digits.Length - 1);
                }
                else
                {
                    digs.RemoveAt(digs.Count - 1);
                }
                return h + new string(digs.Select(d => Digits[d]).ToArray());
            }
            return head + new string(digs.Select(d => Digits[d]).ToArray());
        }
    }
}
