using System;

namespace MonoTouch.SlideoutNavigation
{
    public enum SlideDirection
    {
        Left, Right, Up, Down
    }

    public static class SlideDirectionExtensions
    {
        public static bool IsHorizontal(this SlideDirection direction)
        {
            return direction == SlideDirection.Left || direction == SlideDirection.Right;
        }
    }
}

