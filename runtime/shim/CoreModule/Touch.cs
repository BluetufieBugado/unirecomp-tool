// Phase 3 implementation — Touch struct with mouse-mapped touches.
namespace UnityEngine
{
    public partial struct Touch
    {
        public int fingerId { get; set; }
        public Vector2 position { get; set; }
        public Vector2 deltaPosition { get; set; }
        public TouchPhase phase { get; set; }

        public Touch(int x, int y)
        {
            fingerId = 1;
            position = new Vector2(x, y);
            deltaPosition = new Vector2(0, 0);
            phase = TouchPhase.Stationary;
        }

        public void Update(int newX, int newY)
        {
            Vector2 oldPosition = position;
            position = new Vector2(newX, newY);
            deltaPosition = new Vector2(newX - (int)oldPosition.x, newY - (int)oldPosition.y);

            if (deltaPosition.magnitude > 0)
                phase = TouchPhase.Moved;
            else
                phase = TouchPhase.Stationary;
        }
    }
}
