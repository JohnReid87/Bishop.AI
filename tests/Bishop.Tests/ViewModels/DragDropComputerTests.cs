using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

public sealed class DragDropComputerTests
{
    public sealed class ComputeMoveTargetTests
    {
        [Fact]
        public void WhenDropOnSelf_ReturnsSameIndex()
        {
            DragDropComputer.ComputeMoveTarget(2, 2, 5).Should().Be(2);
        }

        [Fact]
        public void WhenDropAbove_ReturnsTargetIndexUnchanged()
        {
            // Drag item at 3 to insertion point 1 → lands at index 1
            DragDropComputer.ComputeMoveTarget(3, 1, 5).Should().Be(1);
        }

        [Fact]
        public void WhenDropBelow_ReturnsTargetMinusOne()
        {
            // Drag item at 1 to insertion point 4 → lands at index 3 (slot shift)
            DragDropComputer.ComputeMoveTarget(1, 4, 5).Should().Be(3);
        }

        [Fact]
        public void WhenDropAtEnd_ReturnsLastPosition()
        {
            // Drag first item to end of a 5-item list → lands at index 4
            DragDropComputer.ComputeMoveTarget(0, 5, 5).Should().Be(4);
        }

        [Fact]
        public void WhenTargetExceedsCount_ClampsBeforeAdjusting()
        {
            // Overshooting the list end still lands at the last position
            DragDropComputer.ComputeMoveTarget(0, 10, 5).Should().Be(4);
        }

        [Fact]
        public void WhenTargetIsNegative_ClampsToZero()
        {
            DragDropComputer.ComputeMoveTarget(2, -1, 5).Should().Be(0);
        }
    }

    public sealed class ComputeScrollVelocityTests
    {
        [Fact]
        public void WhenCursorInMiddle_ReturnsZero()
        {
            DragDropComputer.ComputeScrollVelocity(300, 600).Should().Be(0);
        }

        [Fact]
        public void WhenCursorAtTopEdgeBoundary_ReturnsZero()
        {
            // posY == edgeZone → neither condition fires
            DragDropComputer.ComputeScrollVelocity(48, 600).Should().Be(0);
        }

        [Fact]
        public void WhenCursorInsideTopEdge_ReturnsNegativeVelocity()
        {
            DragDropComputer.ComputeScrollVelocity(24, 600).Should().BeLessThan(0);
        }

        [Fact]
        public void WhenCursorInsideBottomEdge_ReturnsPositiveVelocity()
        {
            DragDropComputer.ComputeScrollVelocity(576, 600).Should().BeGreaterThan(0);
        }

        [Fact]
        public void WhenCursorFullyAtTop_ReturnsMaxNegativeSpeed()
        {
            // depth = 1 → velocity = -(minSpeed + (maxSpeed - minSpeed) * 1) * tickMs / 1000
            const double expected = -(400.0 + 2600.0) * 16.0 / 1000.0;
            DragDropComputer.ComputeScrollVelocity(0, 600).Should().BeApproximately(expected, 0.001);
        }

        [Fact]
        public void WhenCursorFullyAtBottom_ReturnsMaxPositiveSpeed()
        {
            // depth = 1 → same magnitude as top but positive
            const double expected = (400.0 + 2600.0) * 16.0 / 1000.0;
            DragDropComputer.ComputeScrollVelocity(600, 600).Should().BeApproximately(expected, 0.001);
        }
    }
}
