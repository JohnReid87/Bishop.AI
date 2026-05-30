namespace Bishop.ViewModels;

public static class DragDropComputer
{
    // Given an item dragged from draggedIndex, dropped at insertIndex in a list of
    // count items, returns the final destination index after correcting for the item's
    // own slot shifting when moving downward.
    public static int ComputeMoveTarget(int draggedIndex, int insertIndex, int count)
    {
        var clamped = Math.Clamp(insertIndex, 0, count);
        return clamped > draggedIndex ? clamped - 1 : clamped;
    }

    // Returns the scroll velocity (pixels per timer tick) for auto-scroll during a
    // drag gesture. Negative = scroll up, positive = scroll down, zero = no scroll.
    public static double ComputeScrollVelocity(
        double posY, double viewportHeight,
        double edgeZone = 48.0, double minSpeed = 400.0, double maxSpeed = 3000.0, double tickMs = 16.0)
    {
        if (posY < edgeZone)
        {
            var depth = (edgeZone - posY) / edgeZone;
            return -(minSpeed + (maxSpeed - minSpeed) * depth) * tickMs / 1000.0;
        }
        if (posY > viewportHeight - edgeZone)
        {
            var depth = (posY - (viewportHeight - edgeZone)) / edgeZone;
            return (minSpeed + (maxSpeed - minSpeed) * depth) * tickMs / 1000.0;
        }
        return 0;
    }
}
