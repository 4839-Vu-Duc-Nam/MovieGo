using MovieGoApp.Models;

namespace MovieGoApp.Data;

internal static class SeatSelectionRules
{
    private const int MaxSeatsPerTransaction = 8;
    private const string CoupleType = "Đôi";

    public static void Validate(
        IReadOnlyList<SeatOption> roomSeats,
        IReadOnlyCollection<string> selectedSeatIds,
        bool acceptSeparatedSeats)
    {
        if (selectedSeatIds.Count is < 1 or > MaxSeatsPerTransaction)
            throw new ArgumentException($"Mỗi giao dịch được chọn từ 1 đến {MaxSeatsPerTransaction} ghế.");

        var selectedIds = selectedSeatIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = roomSeats.Where(seat => selectedIds.Contains(seat.Id)).ToArray();
        if (selected.Length != selectedIds.Count || selected.Any(seat => !seat.IsAvailable))
            throw new InvalidOperationException("Một hoặc nhiều ghế không còn trống. Vui lòng tải sơ đồ và chọn lại.");

        var coupleSeats = selected.Where(IsCouple).ToArray();
        if (coupleSeats.Length > 0)
        {
            if (coupleSeats.Length != 2 || selected.Length != 2
                || coupleSeats[0].Row != coupleSeats[1].Row
                || Math.Min(coupleSeats[0].Number, coupleSeats[1].Number) % 2 != 1
                || Math.Abs(coupleSeats[0].Number - coupleSeats[1].Number) != 1)
                throw new ArgumentException("Ghế đôi phải chọn đủ một cặp liền nhau (hai ghế cạnh nhau).");

            var lastRow = roomSeats.OrderBy(seat => seat.Row, StringComparer.OrdinalIgnoreCase).Last().Row;
            if (!string.Equals(coupleSeats[0].Row, lastRow, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Ghế đôi chỉ được bố trí ở hàng cuối cùng của phòng.");
        }

        var oneRow = selected.Select(seat => seat.Row).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
        var consecutive = oneRow
            && selected.Select(seat => seat.Number).Order().Zip(
                selected.Select(seat => seat.Number).Order().Skip(1),
                (left, right) => right == left + 1).All(isConsecutive => isConsecutive);

        if (!consecutive)
        {
            var hasAvailableBlock = roomSeats
                .Where(seat => seat.IsAvailable && !IsCouple(seat))
                .GroupBy(seat => seat.Row, StringComparer.OrdinalIgnoreCase)
                .SelectMany(row => ConsecutiveRuns(row.OrderBy(seat => seat.Number)))
                .Any(run => run >= selected.Length);

            if (hasAvailableBlock || !acceptSeparatedSeats)
                throw new ArgumentException(hasAvailableBlock
                    ? "Vui lòng chọn các ghế liền kề trên cùng một hàng."
                    : "Không còn đủ ghế liền kề. Hãy xác nhận đồng ý chọn ghế rời.");
        }

        foreach (var row in roomSeats.GroupBy(seat => seat.Row, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = row.OrderBy(seat => seat.Number).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                if (!ordered[index].IsAvailable || selectedIds.Contains(ordered[index].Id))
                    continue;

                var blockedOnLeft = index > 0
                    && ordered[index - 1].Number == ordered[index].Number - 1
                    && IsBlocked(ordered[index - 1], selectedIds);
                var blockedOnRight = index + 1 < ordered.Length
                    && ordered[index + 1].Number == ordered[index].Number + 1
                    && IsBlocked(ordered[index + 1], selectedIds);
                var isAisleEdge = index == 0 || index == ordered.Length - 1;
                if ((blockedOnLeft && blockedOnRight)
                    || (isAisleEdge && (blockedOnLeft || blockedOnRight)))
                    throw new ArgumentException("Vui lòng không để trống một ghế đơn lẻ.");
            }
        }
    }

    private static bool IsBlocked(SeatOption seat, IReadOnlySet<string> selectedIds) =>
        !seat.IsAvailable || selectedIds.Contains(seat.Id);

    private static bool IsCouple(SeatOption seat) =>
        string.Equals(seat.Type, CoupleType, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<int> ConsecutiveRuns(IEnumerable<SeatOption> seats)
    {
        var runLength = 0;
        var previousNumber = -2;
        foreach (var seat in seats)
        {
            if (seat.Number != previousNumber + 1)
                runLength = 0;
            runLength++;
            previousNumber = seat.Number;
            yield return runLength;
        }
    }
}
