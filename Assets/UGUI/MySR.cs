using UnityEngine;
using UnityEngine.EventSystems;

public class MySR : UIBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    public enum MovementType
    {
        /// <summary>
        /// Restricted but flexible -- can go past the edges, but springs back in place
        /// </summary>
        Elastic,
        /// <summary>
        /// Restricted movement where it's not possible to go past the edges
        /// </summary>
        Clamped,
    }

    [SerializeField]
    private RectTransform m_Content;
    [SerializeField]
    private bool m_Horizontal = true;
    [SerializeField]
    private bool m_Vertical = true;
    [SerializeField]
    private MovementType m_MovementType = MovementType.Elastic;
    /// <summary>
    /// 弹性系数
    /// </summary>
    [SerializeField]
    private float m_Elasticity = 0.1f; // Only used for MovementType.Elastic
    /// <summary>
    /// 惯性
    /// </summary>
    [SerializeField]
    private bool m_Inertia = true;
    /// <summary>
    /// 减速率
    /// </summary>
    [SerializeField]
    private float m_DecelerationRate = 0.135f; // Only used when inertia is enabled
    [SerializeField]
    private RectTransform m_ViewRect;

    private Vector2 m_PrevPosition = Vector2.zero;
    private Vector2 m_Velocity;
    private bool m_Dragging;

    // The offset from handle position to mouse down position
    private Vector2 m_PointerStartLocalCursor = Vector2.zero;
    private Vector2 m_ContentStartPosition = Vector2.zero;

    private Bounds m_ContentBounds;
    private Bounds m_ViewBounds;

    public virtual void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }
        m_Velocity = Vector2.zero;
    }

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (!IsActive())
        {
            return;
        }

        UpdateBounds();

        m_PointerStartLocalCursor = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_ViewRect, eventData.position, eventData.pressEventCamera, out m_PointerStartLocalCursor);
        m_ContentStartPosition = m_Content.anchoredPosition;
        m_Dragging = true;
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }
        m_Dragging = false;
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (!IsActive())
        {
            return;
        }

        //TODO:这一步做什么用？确保在同一个平面？
        Vector2 localCursor;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_ViewRect, eventData.position, eventData.pressEventCamera, out localCursor))
        {
            return;
        }
        
        UpdateBounds();

        var pointerDelta = localCursor - m_PointerStartLocalCursor;
        Vector2 position = m_ContentStartPosition + pointerDelta;

        // Offset to get content into place in the view.
        Vector2 offset = CalculateOffset(position - m_Content.anchoredPosition);
        position += offset;
        if (m_MovementType == MovementType.Elastic)
        {
            if (offset.x != 0)
            {
                position.x = position.x - RubberDelta(offset.x, m_ViewBounds.size.x);
            }
            if (offset.y != 0)
            {
                position.y = position.y - RubberDelta(offset.y, m_ViewBounds.size.y);
            }
        }

        SetContentAnchoredPosition(position);
    }

    protected virtual void SetContentAnchoredPosition(Vector2 position)
    {
        if (!m_Horizontal)
        {
            position.x = m_Content.anchoredPosition.x;
        }
        if (!m_Vertical)
        {
            position.y = m_Content.anchoredPosition.y;
        }

        if (position != m_Content.anchoredPosition)
        {
            m_Content.anchoredPosition = position;
            UpdateBounds();
        }
    }

    private static float RubberDelta(float overStretching, float viewSize)
    {
        return (1 - (1 / ((Mathf.Abs(overStretching) * 0.55f / viewSize) + 1))) * viewSize * Mathf.Sign(overStretching);
    }

    private void UpdateBounds()
    {
        m_ViewBounds = new Bounds(m_ViewRect.rect.center, m_ViewRect.rect.size);
        m_ContentBounds = GetBounds();
        //注意，m_ContentBounds不能和m_ViewBounds一样直接获得，不然求出的m_ContentBounds.min是在本地坐标下的，不是相对m_ViewRect的
    }

    private readonly Vector3[] m_Corners = new Vector3[4];
    private Bounds GetBounds()
    {
        if (m_Content == null)
        {
            return new Bounds();
        }

        var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        var toLocal = m_ViewRect.worldToLocalMatrix;
        m_Content.GetWorldCorners(m_Corners);
        for (int j = 0; j < 4; j++)
        {
            Vector3 v = toLocal.MultiplyPoint3x4(m_Corners[j]);
            vMin = Vector3.Min(v, vMin);
            vMax = Vector3.Max(v, vMax);
        }

        var bounds = new Bounds(vMin, Vector3.zero);
        bounds.Encapsulate(vMax);
        return bounds;
    }

    /// <summary>
    /// 计算超出偏移
    /// </summary>
    /// <param name="delta"></param>
    /// <returns></returns>
    private Vector2 CalculateOffset(Vector2 delta)
    {
        Vector2 offset = Vector2.zero;

        Vector2 min = m_ContentBounds.min;
        Vector2 max = m_ContentBounds.max;

        if (m_Horizontal)
        {
            min.x += delta.x;
            max.x += delta.x;
            if (min.x > m_ViewBounds.min.x)
            {
                offset.x = m_ViewBounds.min.x - min.x;
            }
            else if (max.x < m_ViewBounds.max.x)
            {
                offset.x = m_ViewBounds.max.x - max.x;
            }
        }

        if (m_Vertical)
        {
            min.y += delta.y;
            max.y += delta.y;
            if (max.y < m_ViewBounds.max.y)
            {
                offset.y = m_ViewBounds.max.y - max.y;
            }
            else if (min.y > m_ViewBounds.min.y)
            {
                offset.y = m_ViewBounds.min.y - min.y;
            }
        }

        return offset;
    }

    /// <summary>
    /// 处理缓动
    /// </summary>
    protected virtual void LateUpdate()
    {
        UpdateBounds();
        float deltaTime = Time.unscaledDeltaTime;
        Vector2 offset = CalculateOffset(Vector2.zero);
        if (!m_Dragging && (offset != Vector2.zero || m_Velocity != Vector2.zero))
        {
            Vector2 position = m_Content.anchoredPosition;
            for (int axis = 0; axis < 2; axis++)
            {
                // Apply spring physics if movement is elastic and content has an offset from the view.
                if (m_MovementType == MovementType.Elastic && offset[axis] != 0)
                {
                    float speed = m_Velocity[axis];
                    position[axis] = Mathf.SmoothDamp(m_Content.anchoredPosition[axis], m_Content.anchoredPosition[axis] + offset[axis], ref speed, m_Elasticity, Mathf.Infinity, deltaTime);
                    m_Velocity[axis] = speed;
                }
                // Else move content according to velocity with deceleration applied.
                else if (m_Inertia)
                {
                    m_Velocity[axis] *= Mathf.Pow(m_DecelerationRate, deltaTime);
                    if (Mathf.Abs(m_Velocity[axis]) < 1)
                    {
                        m_Velocity[axis] = 0;
                    }
                    position[axis] += m_Velocity[axis] * deltaTime;
                }
                // If we have neither elaticity or friction, there shouldn't be any velocity.
                else
                {
                    m_Velocity[axis] = 0;
                }
            }

            if (m_Velocity != Vector2.zero)
            {
                if (m_MovementType == MovementType.Clamped)
                {
                    offset = CalculateOffset(position - m_Content.anchoredPosition);
                    position += offset;
                }

                SetContentAnchoredPosition(position);
            }
        }

        if (m_Dragging && m_Inertia)
        {
            Vector3 newVelocity = (m_Content.anchoredPosition - m_PrevPosition) / deltaTime;
            m_Velocity = Vector3.Lerp(m_Velocity, newVelocity, deltaTime * 10);
        }

        if (m_Content.anchoredPosition != m_PrevPosition)
        {
            UpdatePrevData();
        }
    }

    private void UpdatePrevData()
    {
        if (m_Content == null)
        {
            m_PrevPosition = Vector2.zero;
        }
        else
        {
            m_PrevPosition = m_Content.anchoredPosition;
        }
    }
}