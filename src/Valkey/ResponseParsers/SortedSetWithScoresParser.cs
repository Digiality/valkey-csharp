using Valkey.Protocol;

namespace Valkey.ResponseParsers;

/// <summary>
/// Parses ZRANGE WITHSCORES responses, handling both RESP2 and RESP3 formats.
/// </summary>
/// <remarks>
/// RESP3 returns array of [member, score] pairs: [[member1, score1], [member2, score2], ...]
/// RESP2 returns flat array: [member1, score1, member2, score2, ...]
/// </remarks>
internal static class SortedSetWithScoresParser
{
    /// <summary>
    /// Parses a ZRANGE WITHSCORES response into an array of (member, score) tuples.
    /// </summary>
    /// <param name="response">The RESP response value containing the sorted set members with scores.</param>
    /// <returns>An array of tuples containing member names and their scores.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the response format is invalid.</exception>
    public static (string member, double score)[] Parse(RespValue response)
    {
        if (!response.TryGetArray(out var array))
        {
            throw new InvalidOperationException($"ZRANGE WITHSCORES: Expected array response, got {response.Type}");
        }

        var result = new List<(string member, double score)>(array.Length / 2);

        // Detect format by checking if first element is an array (RESP3) or scalar (RESP2)
        if (array.Length > 0 && array[0].TryGetArray(out _))
        {
            // RESP3 format: array of [member, score] pairs
            ParseResp3Format(array, result);
        }
        else
        {
            // RESP2 format: flat array [member, score, member, score, ...]
            ParseResp2Format(array, result);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Parses RESP3 format: [[member1, score1], [member2, score2], ...]
    /// </summary>
    private static void ParseResp3Format(RespValue[] array, List<(string member, double score)> result)
    {
        foreach (var pair in array)
        {
            if (!pair.TryGetArray(out var pairArray))
            {
                throw new InvalidOperationException(
                    $"ZRANGE WITHSCORES (RESP3): Expected array pair, got {pair.Type}");
            }

            if (pairArray.Length != 2)
            {
                throw new InvalidOperationException(
                    $"ZRANGE WITHSCORES (RESP3): Expected pair with 2 elements, got {pairArray.Length}");
            }

            if (!pairArray[0].TryGetString(out var member))
            {
                throw new InvalidOperationException(
                    $"ZRANGE WITHSCORES (RESP3): Expected string member, got {pairArray[0].Type}");
            }

            var score = ParseScore(pairArray[1], "RESP3");
            result.Add((member!, score));
        }
    }

    /// <summary>
    /// Parses RESP2 format: [member1, score1, member2, score2, ...]
    /// </summary>
    private static void ParseResp2Format(RespValue[] array, List<(string member, double score)> result)
    {
        if (array.Length % 2 != 0)
        {
            throw new InvalidOperationException(
                $"ZRANGE WITHSCORES (RESP2): Expected even-length array for member-score pairs, got {array.Length} elements");
        }

        for (int i = 0; i < array.Length; i += 2)
        {
            if (!array[i].TryGetString(out var member))
            {
                throw new InvalidOperationException(
                    $"ZRANGE WITHSCORES (RESP2): Expected string member at index {i}, got {array[i].Type}");
            }

            var score = ParseScore(array[i + 1], "RESP2");
            result.Add((member!, score));
        }
    }

    /// <summary>
    /// Parses a score value, handling both double and string representations.
    /// </summary>
    private static double ParseScore(RespValue scoreValue, string format)
    {
        // Try to get as double directly (RESP3 may send as number)
        if (scoreValue.TryGetDouble(out var doubleScore))
        {
            return doubleScore;
        }

        // Fallback: parse from string (RESP2 sends as string)
        if (scoreValue.TryGetString(out var scoreStr))
        {
            if (double.TryParse(scoreStr, out var parsedScore))
            {
                return parsedScore;
            }

            throw new InvalidOperationException(
                $"ZRANGE WITHSCORES ({format}): Invalid score format '{scoreStr}'");
        }

        throw new InvalidOperationException(
            $"ZRANGE WITHSCORES ({format}): Expected double or string score, got {scoreValue.Type}");
    }
}
