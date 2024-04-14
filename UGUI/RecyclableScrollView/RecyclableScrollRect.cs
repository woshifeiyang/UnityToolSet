using System;
using System.Collections;
using System.Collections.Generic;
using XLua;

namespace UnityEngine.UI
{
    [RequireComponent(typeof(RectTransform))]
    [LuaCallCSharp]
    public class RecyclableScrollRect : ScrollRect
    {
        #region 变量
        [SerializeField]
        private RectTransform m_Item;
        public RectTransform Item { get { return m_Item; } set { m_Item = value; } }

        public enum dirType
        {
            Vertical = 0,
            Horizontal = 1,
        }
        [SerializeField]
        private dirType m_Direction;
        public dirType Direction { get { return m_Direction; } set { m_Direction = value; } }

        [SerializeField]
        private bool m_IsGrid;
        public bool IsGrid { get { return m_IsGrid; } set { m_IsGrid = value; } }

        [SerializeField]
        private int m_Col;
        public int Col { get { return m_Col; } set { m_Col = value; } }

        [SerializeField]
        private int m_Row;
        public int Row { get { return m_Row; } set { m_Row = value; } }

        [SerializeField]
        private float m_Spacing;
        public float Spacing { get { return m_Spacing; } set { m_Spacing = value; } }

        [SerializeField]
        private Vector2 m_GridSpacing;
        public Vector2 GridSpacing { get { return m_GridSpacing; } set { m_GridSpacing = value; } }

        [SerializeField]
        private RectOffset m_Padding;
        public RectOffset Padding { get { return m_Padding; } set { m_Padding = value; } }

        [SerializeField]
        private GameObject m_ItemPool;

        private bool m_IsFull = false;

        #endregion

        #region 私有变量
        private List<RectTransform> m_Caches = new List<RectTransform>();
        private float m_spacing;
        private int m_cellNum;

        private Action<int, GameObject> callBackAction;

        [CSharpCallLua]
        public delegate void CallBackActionCellDelegate(int index, int uniqueId, GameObject go);

        private CallBackActionCellDelegate callBackActionCell;

        // 延时显示部分
        private bool m_IsDelay = false;
        private WaitForSeconds m_ColDelayTime;
        private float m_ColDelaySingleTime;
        private WaitForSeconds m_RowDelayTime;
        private float m_RowDelaySingleTime;

        // 生成的行数和列数
        private int m_ViewportItemRow;
        private int m_ViewportItemCol;

        private int m_LogicItemRow;
        private int m_LogicItemCol;

        // 动态列表容器
        private List<RectTransform> m_ArrayList = new List<RectTransform>();
        private int m_ItemFromIndex;
        private int m_ItemToIndex;

        // 记录滑动
        float m_LastPos = 0.0f;
        float m_NormalDistanceHeight;
        float m_NormalDistanceWidth;
        int m_CurIndex = 0;
        int m_CurEndIndex = 0;

        // 无限策略
        bool m_IsRecyclable = false;
        int m_StartIndex = 1;

        int m_TopDataIndex = 0;
        int m_BotDataIndex = 0;

        #endregion

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!viewport)
            {
                GameObject go = new GameObject();
                viewport = go.AddComponent<RectTransform>();
                viewport.gameObject.name = "Viewport";
                viewport.SetParent(this.transform);
                viewport.gameObject.AddComponent<RectMask2D>();
                viewport.localScale = Vector3.one;
            }

            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = Vector2.zero;
            viewRect.offsetMax = Vector2.zero;
            viewport.anchoredPosition = Vector3.zero;

            if (!content)
            {
                GameObject go = new GameObject();
                content = go.AddComponent<RectTransform>();
                content.gameObject.name = "Content";
                content.SetParent(viewport.transform);
                content.localScale = Vector3.one;
            }

            if (Direction == dirType.Vertical)
            {
                content.anchorMin = new Vector2(0, 1);
                content.anchorMax = new Vector2(1, 1);
                content.pivot = new Vector2(0.5f, 1);
            }
            else
            {
                content.anchorMin = new Vector2(0, 0);
                content.anchorMax = new Vector2(0, 1);
                content.pivot = new Vector2(0, 0.5f);
            }
            // content.offsetMin = new Vector2(0, 0);
            // content.offsetMax = new Vector2(0, 0);
            content.anchoredPosition3D = Vector3.zero;

            // 添加一个缓存池
            if (!m_ItemPool)
            {
                m_ItemPool = new GameObject();
                m_ItemPool.AddComponent<RectTransform>();
                m_ItemPool.name = "CachePool";
                m_ItemPool.transform.parent = transform;
                m_ItemPool.SetActive(false);
            }

            vertical = m_Direction == dirType.Vertical;
            horizontal = m_Direction == dirType.Horizontal;

            LayoutDelayShow lds = gameObject.GetComponent<LayoutDelayShow>();
            if (lds != null)
            {
                m_IsDelay = true;

                if (lds.colDelay == 0.0f)
                {
                    m_ColDelayTime = null;
                    m_ColDelaySingleTime = 0.0f;
                }
                else
                {
                    m_ColDelayTime = new WaitForSeconds(lds.colDelay);
                    m_ColDelaySingleTime = lds.colDelay;
                }

                if (lds.rowDelay == 0.0f)
                {
                    m_RowDelayTime = null;
                    m_RowDelaySingleTime = 0.0f;
                }
                else
                {
                    m_RowDelayTime = new WaitForSeconds(lds.rowDelay);
                    m_RowDelaySingleTime = lds.rowDelay;
                }

            }
            else
            {
                m_IsDelay = false;
                m_RowDelayTime = null;
                m_RowDelaySingleTime = 0.0f;
                m_ColDelayTime = null;
                m_ColDelaySingleTime = 0.0f;
            }
        }

        #region GridLayout部分

        private void CalculateContentSize()
        {
            float viewPortWidth = viewport.rect.width;
            float viewPortHeight = viewport.rect.height;
            float contentWidth = viewport.rect.width;
            float contentHeight = viewport.rect.height;
            float itemWidth = m_Item.rect.width;
            float itemHeight = m_Item.rect.height;

            contentWidth = contentWidth - m_Padding.left - m_Padding.right;
            contentHeight = contentHeight - m_Padding.top - m_Padding.bottom;

            if (!m_IsGrid)
            {
                if (m_Direction == dirType.Vertical)
                {
                    m_Col = 1;
                    m_GridSpacing = new Vector2(0.0f, m_Spacing);
                }
                else
                {
                    m_Row = 1;
                    m_GridSpacing = new Vector2(m_Spacing, 0.0f);
                }
            }

            bool needExpand = false;
            if (m_Direction == dirType.Vertical)
            {
                if (m_Col <= 0)
                {
                    m_Col = Mathf.FloorToInt((viewPortWidth - m_Padding.left - m_Padding.right + m_GridSpacing.x) / (itemWidth + m_GridSpacing.x));
                }
                m_ViewportItemCol = m_Col;
                m_LogicItemCol = m_Col;
                int row = Mathf.FloorToInt((viewPortHeight - m_Padding.top - m_Padding.bottom + m_GridSpacing.y) / (itemHeight + m_GridSpacing.y));
                if (m_ViewportItemCol * row >= m_cellNum)
                {
                    m_ViewportItemRow = row;
                }
                else
                {
                    m_ViewportItemRow = Mathf.CeilToInt((contentHeight + m_GridSpacing.y) / (itemHeight + m_GridSpacing.y));
                    needExpand = true;
                }
            }
            else
            {
                if (m_Row <= 0)
                {
                    m_Row = Mathf.FloorToInt((viewPortHeight - m_Padding.top - m_Padding.bottom + m_GridSpacing.y) / (itemHeight + m_GridSpacing.y));
                }
                m_ViewportItemRow = m_Row;
                m_LogicItemRow = m_Row;
                int col = Mathf.FloorToInt((viewPortWidth - m_Padding.left - m_Padding.right + m_GridSpacing.x) / (itemWidth + m_GridSpacing.x));
                if (m_ViewportItemRow * col >= m_cellNum)
                {
                    m_ViewportItemCol = col;
                }
                else
                {
                    m_ViewportItemCol = Mathf.CeilToInt((contentWidth + m_GridSpacing.x) / (itemWidth + m_GridSpacing.x));
                    needExpand = true;
                }
            }

            if (needExpand)
            {
                // 需要扩大View大小
                if (m_Direction == dirType.Vertical)
                {
                    if (m_IsFull)
                    {
                        m_cellNum = FindNearestMultiple(m_cellNum, m_Col);
                    }

                    m_LogicItemRow = m_cellNum / m_Col;

                    if ((m_cellNum % m_Col) > 0)
                    {
                        ++m_LogicItemRow;
                    }

                    if (m_ViewportItemRow * 2 > m_LogicItemRow)
                    {
                        m_ViewportItemRow = m_LogicItemRow;
                        m_IsRecyclable = false;
                    }
                    else
                    {
                        m_ViewportItemRow *= 2;
                        m_IsRecyclable = true;
                    }
                }
                else
                {
                    if (m_IsFull)
                    {
                        m_cellNum = FindNearestMultiple(m_cellNum, m_Row);
                    }

                    m_LogicItemCol = m_cellNum / m_Row;

                    if ((m_cellNum % m_Row) > 0)
                    {
                        ++m_LogicItemCol;
                    }

                    if (m_ViewportItemCol * 2 > m_LogicItemCol)
                    {
                        m_ViewportItemCol = m_LogicItemCol;
                        m_IsRecyclable = false;
                    }
                    else
                    {
                        m_ViewportItemCol *= 2;
                        m_IsRecyclable = true;
                    }
                }
            }
            else
            {
                if (m_IsFull)
                {
                    m_cellNum = m_ViewportItemRow * m_ViewportItemCol;
                }
                m_LogicItemRow = m_ViewportItemRow;
                m_LogicItemCol = m_ViewportItemCol;

                m_IsRecyclable = false;
            }

            contentWidth = m_LogicItemCol * itemWidth + (m_LogicItemCol - 1) * m_GridSpacing.x;
            contentHeight = m_LogicItemRow * itemHeight + (m_LogicItemRow - 1) * m_GridSpacing.y;

            contentWidth = contentWidth + m_Padding.left + m_Padding.right;
            contentHeight = contentHeight + m_Padding.top + m_Padding.bottom;

            if (m_Direction == dirType.Vertical)
            {
                Vector3 pos = content.anchoredPosition3D;
                content.sizeDelta = new Vector2(0, contentHeight);
                pos.x = 0;
                content.anchoredPosition3D = pos;
            }
            else
            {
                Vector3 pos = content.anchoredPosition3D;
                content.sizeDelta = new Vector2(contentWidth, 0);
                pos.y = 0;
                content.anchoredPosition3D = pos;

            }

            m_BotDataIndex = 1;
            m_TopDataIndex = m_cellNum;
        }

        private void CreateCellList()
        {
            int showItemNum = Math.Min(m_cellNum, m_ViewportItemRow * m_ViewportItemCol);

            int curItemNum = m_ArrayList.Count;

            while (showItemNum > curItemNum)
            {
                // 添加列表
                RectTransform itemRect = CreateCellItem();
                itemRect.SetParent(content);
                itemRect.localScale = Vector3.one;
                itemRect.anchoredPosition3D = Vector3.zero;
                m_ArrayList.Add(itemRect);

                ++curItemNum;

                itemRect.gameObject.SetActive(true);
            }

            while (showItemNum < curItemNum)
            {
                //删除列表
                RectTransform itemRect = m_ArrayList[curItemNum - 1];

                itemRect.gameObject.SetActive(false);
                RemoveCellItem(itemRect);
                m_ArrayList.RemoveAt(curItemNum - 1);
                --curItemNum;
            }

            int count = m_ArrayList.Count;
            for (int i = 0; i < count; ++i)
            {
                Vector2Int indexPos = CalculateItemPosIndex(i);

                m_ArrayList[i].anchoredPosition3D = CalculateItemPos(indexPos);
                m_ArrayList[i].name = "Cell" + i;
                if (m_IsDelay)
                {
                    StartCoroutine(DelayShow(m_ArrayList[i].gameObject, indexPos.x, indexPos.y));
                }
            }
            
            if (m_Direction == dirType.Vertical)
            {
                m_ItemFromIndex = 0;
                m_ItemToIndex = m_ViewportItemRow * m_ViewportItemCol - 1;

                m_CurIndex = 0;
                m_CurEndIndex = m_ViewportItemRow - 1;
            }
            else
            {
                m_ItemFromIndex = 0;
                m_ItemToIndex = m_ViewportItemRow * m_ViewportItemCol - 1;

                m_CurIndex = 0;
                m_CurEndIndex = m_ViewportItemCol - 1;
            }

            if (m_IsRecyclable)
            {
                m_StartIndex = 2;
            }
        }

        private Vector2Int CalculateItemPosIndex(int index)
        {
            Vector2Int res;

            if (m_IsGrid)
            {
                int rowIndex;
                int colIndex;
                if (m_Direction == dirType.Vertical)
                {
                    rowIndex = index / m_ViewportItemCol;
                    colIndex = index % m_ViewportItemCol;
                }
                else
                {
                    colIndex = index / m_ViewportItemRow;
                    rowIndex = index % m_ViewportItemRow;
                }

                res = new Vector2Int(colIndex, rowIndex);
            }
            else
            {
                if (m_Direction == dirType.Vertical)
                {
                    res = new Vector2Int(0, index);
                }
                else
                {
                    res = new Vector2Int(index, 0);
                }
            }

            return res;
        }

        private Vector2 CalculateItemPos(int index)
        {
            Vector2 res;

            if (m_IsGrid)
            {
                int rowIndex;
                int colIndex;
                if (m_Direction == dirType.Vertical)
                {
                    rowIndex = index / m_ViewportItemCol;
                    colIndex = index % m_ViewportItemCol;
                }
                else
                {
                    colIndex = index / m_ViewportItemRow;
                    rowIndex = index % m_ViewportItemRow;
                }

                float posX = CalculateItemPosX(colIndex);
                float posY = CalculateItemPosY(rowIndex);
                res = new Vector2(posX, posY);
            }
            else
            {
                float posX;
                float posY;
                if (m_Direction == dirType.Vertical)
                {
                    posX = CalculateItemPosX(0);
                    posY = CalculateItemPosY(index);
                    res = new Vector2(posX, posY);
                }
                else
                {
                    posX = CalculateItemPosX(index);
                    posY = CalculateItemPosY(0);
                    res = new Vector2(posX, posY);
                }

            }

            return res;
        }

        private Vector2 CalculateItemPos(Vector2Int index)
        {
            return new Vector2(CalculateItemPosX(index.x), CalculateItemPosY(index.y));
        }

        private float CalculateItemPosX(int colIndex)
        {
            return m_Padding.left + colIndex * (m_Item.rect.width + m_GridSpacing.x);
        }

        private float CalculateItemPosY(int rowIndex)
        {
            return -(rowIndex * (m_Item.rect.height + m_GridSpacing.y) + m_Padding.top);
        }

        private RectTransform CreateCellItem()
        {
            GameObject itemGo;
            GameObject parentGO;
            RectTransform parentRect;

            if (m_ItemPool.transform.childCount > 0)
            {
                parentGO = m_ItemPool.transform.GetChild(0).gameObject;
                parentRect = parentGO.GetComponent<RectTransform>();
                itemGo = parentGO.transform.GetChild(0).gameObject;
            }
            else
            {
                itemGo = GameObject.Instantiate(m_Item.gameObject);
                parentGO = new GameObject();
                parentRect = parentGO.AddComponent<RectTransform>();
            }

            parentRect.anchorMin = new Vector2(0, 1.0f);
            parentRect.anchorMax = new Vector2(0, 1.0f);
            parentRect.pivot = new Vector2(0f, 1.0f);

            RectTransform itemRect = itemGo.GetComponent<RectTransform>();
            itemRect.SetParent(parentRect);
            itemGo.SetActive(true);

            parentRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, itemRect.sizeDelta.y);

            itemRect.anchoredPosition3D = Vector3.zero;
            itemRect.localScale = Vector3.one;

            parentGO.SetActive(false);
            return parentRect;
        }

        private void RemoveCellItem(RectTransform itemRect)
        {
            itemRect.SetParent(m_ItemPool.transform);
        }

        IEnumerator DelayShow(GameObject go, int colCount, int rowCount)
        {
            if (go != null)
            {
                go.SetActive(false);
            }

            if (m_RowDelayTime != null)
            {
                for (int i = 0; i < rowCount; ++i)
                {
                    yield return m_RowDelayTime;
                }
            }

            if (m_ColDelayTime != null)
            {
                for (int i = 0; i < colCount; ++i)
                {
                    yield return m_ColDelayTime;
                }
            }

            if (go != null)
            {
                go.SetActive(true);
            }
        }

        #endregion

        #region 对外API
        [CSharpCallLua]
        public void SetCellNum(int num, bool isReset = false)
        {
            m_cellNum = num;

            if (isReset)
            {
                SetContentAnchoredPosition(Vector2.zero);
            }

            CalculateContentSize();

            CreateCellList();

            if (callBackAction != null)
            {
                int listNum = m_ArrayList.Count;

                for (int i = 0; i < listNum; i++)
                {
                    callBackAction(i + 1, m_ArrayList[i].GetChild(0).gameObject);
                }
            }

            if (callBackActionCell != null)
            {
                int listNum = m_ArrayList.Count;

                for (int i = 0; i < listNum; i++)
                {
                    callBackActionCell(i + 1, m_ArrayList[i].GetInstanceID(), m_ArrayList[i].GetChild(0).gameObject);
                }
            }

            onValueChanged.RemoveAllListeners();
            onValueChanged.AddListener(OnValueChange);

            if (m_Direction == dirType.Vertical)
            {
                m_LastPos = content.anchoredPosition.y;
            }
            else
            {
                m_LastPos = content.anchoredPosition.x;
            }

            m_NormalDistanceHeight = m_Item.rect.height + m_GridSpacing.y;
            m_NormalDistanceWidth = m_Item.rect.width + m_GridSpacing.x;
        }

        [CSharpCallLua]
        public bool CheckIndexState(int index)
        {
            if (index >= m_BotDataIndex && index <= m_TopDataIndex)
                return true;
            else
                return false;
        }

        // 设置Lua回调
        [CSharpCallLua]
        public void SetLuaCallBack(Action<int, GameObject> action)
        {
            callBackAction = action; 
        }

        // 设置带有CellId的Lua回调
        [CSharpCallLua]
        public void SetCellLuaCallBack(CallBackActionCellDelegate action)
        {
            callBackActionCell = action;
        }

        // 刷新当前显示的列表
        [CSharpCallLua]
        public void RefreshList()
        {

        }

        [CSharpCallLua]
        public void SetItem(RectTransform item)
        {
            m_Item = item;
            CleanAllCell();
        }

        [CSharpCallLua]
        public void HideAllCell()
        {
            for (int i = 0; i < m_ArrayList.Count; ++i)
            {
                m_ArrayList[i].gameObject.SetActive(false);
            }
        }

        [CSharpCallLua]
        public void CleanAllCell()
        {
            for (int i = m_ArrayList.Count - 1; i >= 0; i--)
            {
                Destroy(m_ArrayList[i].gameObject);
                m_ArrayList.RemoveAt(i);
            }

            for (int i = m_Caches.Count - 1; i >= 0; i--)
            {
                Destroy(m_Caches[i].gameObject);
                m_Caches.RemoveAt(i);
            }
        }

        /*
        // 指定位置添加一个额外的item
        [CSharpCallLua]
        public void AddItemByIndex(GameObject obj, int idx)
        {
        }
        */

        // 重置当前的列表
        [CSharpCallLua]
        public void ResetList()
        {
            SetCellNum(0);
        }

        [CSharpCallLua]
        public void SetIsFull(bool isFull)
        {
            m_IsFull = isFull;
        }


        /// <summary>
        /// 要跳转哪一个Index的Item
        /// </summary>
        /// <param name="index"></param>
        /// <summary>
        /// 跳转后的位置 位于显示的多少行
        /// </summary>
        /// <param name="cellIndex"></param>
        [CSharpCallLua]
        public void JumpToIndex(int index, int cellIndex = 1)
        {
            Vector2 itemPos = CalculateItemPos(index);
            if (Direction == dirType.Vertical)
            {
                float offset = (Item.rect.height * (cellIndex - 1));
                SetContentAnchoredPosition(new Vector2(0, -itemPos.y - offset));
            }
            else if (Direction == dirType.Horizontal)
            {
                float offset = (Item.rect.width * (cellIndex - 1));
                SetContentAnchoredPosition(new Vector2(-itemPos.x + offset, 0));
            }

            OnValueChange(itemPos);
        }

        //获取topDataIndex
        [CSharpCallLua]
        public int GetTopDataIndex()
        {
            return m_TopDataIndex;
        }

        //botDataIndex
        [CSharpCallLua]
        public int GetBotDataIndex()
        {
            return m_BotDataIndex;
        }
        #endregion

        #region 内部循环

        private void OnValueChange(Vector2 normalizedPosition)
        {
            if (!m_IsRecyclable)
            {
                return;
            }
            if (m_Direction == dirType.Vertical)
            {
                // 正数代表向下滑动 负数代表向上滑动
                float dis = m_LastPos - content.anchoredPosition.y;
                
                if (Mathf.Abs(dis) >= m_NormalDistanceHeight)
                {
                    if (dis > 0)
                    {
                        // 向下滑动 BottomToUp
                        int num = Mathf.FloorToInt(dis / m_NormalDistanceHeight);
                        for (int i = 0; i < num; ++i)
                        {
                            m_LastPos -= m_NormalDistanceHeight;
                            if (m_LastPos < 0)
                            {
                                m_LastPos = 0;
                                break;
                            }
                            --m_CurIndex;
                            --m_CurEndIndex;
                            
                            if ((m_LogicItemRow - m_CurEndIndex) >= (m_StartIndex - 2))
                            {
                                bool flag = ChangeBotToTop();
                            }
                        }
                    }
                    else
                    {
                        // 向上滑动 UpToBottom
                        int num = Mathf.FloorToInt(-dis / m_NormalDistanceHeight);
                        for (int i = 0; i < num; ++i)
                        {
                            m_LastPos += m_NormalDistanceHeight;
                            
                            if (Direction == dirType.Vertical && m_LastPos > content.rect.height)
                            {
                                m_LastPos = content.rect.height;
                                break;
                            }

                            ++m_CurIndex;
                            ++m_CurEndIndex;
                            
                            if (m_CurIndex > m_StartIndex)
                            {
                                bool flag = ChangeTopToBot();
                            }
                        }
                    }
                }
            }
            else
            {
                // 正数代表向左滑动 负数代表向右滑动
                float dis = m_LastPos - content.anchoredPosition.x;
                if (Mathf.Abs(dis) >= m_NormalDistanceWidth)
                {
                    if (dis > 0)
                    {
                        // 向左滑动 LeftToRight
                        int num = Mathf.FloorToInt(dis / m_NormalDistanceWidth);
                        for (int i = 0; i < num; ++i)
                        {
                            m_LastPos -= m_NormalDistanceWidth;
                            if (Direction == dirType.Horizontal && m_LastPos < (-(content.rect.width - viewRect.rect.width)))
                            {
                                m_LastPos = (-(content.rect.width - viewRect.rect.width));
                                break;
                            }


                            ++m_CurIndex;
                            ++m_CurEndIndex;
                            if (m_CurIndex > m_StartIndex)
                            {
                                bool flag = ChangeLeftToRight();
                            }
                        }
                    }
                    else
                    {
                        // 向右滑动 RightToLeft
                        int num = Mathf.FloorToInt(-dis / m_NormalDistanceWidth);
                        for (int i = 0; i < num; ++i)
                        {
                            m_LastPos += m_NormalDistanceWidth;
                            if (m_LastPos > 0)
                            {
                                m_LastPos = 0;
                                break;
                            }

                            --m_CurIndex;
                            --m_CurEndIndex;
                            if ((m_LogicItemCol - m_CurEndIndex) >= (m_StartIndex - 2))
                            {
                                bool flag = ChangeRightToLeft();
                            }
                        }
                    }
                }
            }
        }

        private List<RectTransform> moves = new List<RectTransform>();

        private bool ChangeTopToBot()
        {
            moves.Clear();

            if (m_Direction == dirType.Vertical)
            {
                if (m_ItemToIndex + 1 >= m_cellNum)
                {
                    return false;
                }

                if (m_ItemToIndex + m_ViewportItemCol >= m_cellNum)
                {
                    RemoveViewItems(0, m_ItemToIndex + m_ViewportItemCol - m_cellNum + 1, ref moves);
                }
                else
                {
                    RemoveViewItems(0, m_ViewportItemCol, ref moves);
                }
                
                if (moves == null || moves.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < moves.Count; i++)
                {
                    ++m_ItemFromIndex;
                    ++m_ItemToIndex;
                    moves[i].anchoredPosition3D = CalculateItemPos(m_ItemToIndex);
                    if (callBackAction != null)
                    {
                        callBackAction(m_ItemToIndex + 1, moves[i].GetChild(0).gameObject);
                    }

                    if (callBackActionCell != null)
                    {
                        callBackActionCell(m_ItemToIndex + 1, moves[i].GetInstanceID(),
                            moves[i].GetChild(0).gameObject);
                    }
                    
                    m_ArrayList.Add(moves[i]);
                }
                return true;
            }

            return false;
        }

        private bool ChangeBotToTop()
        {
            moves.Clear();

            if (m_Direction == dirType.Vertical)
            {
                if (m_ItemFromIndex <= 0)
                {
                    return false;
                }

                if ((m_ItemToIndex + 1) % m_ViewportItemCol != 0)
                {
                    int k = FindNearestMultiple(m_ItemToIndex + 1, m_ViewportItemCol) - m_ViewportItemCol;
                    k = m_ItemToIndex - (k - 1);
                    RemoveViewItems(m_ArrayList.Count - k, k, ref moves);
                }
                else
                {
                    RemoveViewItems(m_ArrayList.Count - m_ViewportItemCol, m_ViewportItemCol, ref moves);
                }

                if (moves == null || moves.Count == 0)
                {
                    return false;
                }
                
                for (int i = moves.Count - 1; i >= 0; i--)
                {
                    --m_ItemFromIndex;
                    --m_ItemToIndex;
                    moves[i].anchoredPosition3D = CalculateItemPos(m_ItemFromIndex);
                    
                    if (callBackAction != null)
                    {
                        callBackAction(m_ItemFromIndex + 1, moves[i].GetChild(0).gameObject);
                    }

                    if (callBackActionCell != null)
                    {
                        callBackActionCell(m_ItemToIndex + 1, moves[i].GetInstanceID(),
                            moves[i].GetChild(0).gameObject);
                    }
                }

                m_ArrayList.InsertRange(0, moves);

                return true;
            }

            return false;
        }

        private bool ChangeLeftToRight()
        {
            moves.Clear();

            if (m_Direction == dirType.Horizontal)
            {
                if (m_ItemToIndex + 1 >= m_cellNum)
                {
                    return false;
                }

                if (m_ItemToIndex + m_ViewportItemRow >= m_cellNum)
                {
                    RemoveViewItems(0, m_ItemToIndex + m_ViewportItemRow - m_cellNum + 1, ref moves);
                }
                else
                {
                    RemoveViewItems(0, m_ViewportItemRow, ref moves);
                }

                if (moves == null || moves.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < moves.Count; i++)
                {
                    ++m_ItemFromIndex;
                    ++m_ItemToIndex;
                    moves[i].anchoredPosition3D = CalculateItemPos(m_ItemToIndex);
                    if (callBackAction != null)
                    {
                        callBackAction(m_ItemToIndex + 1, moves[i].GetChild(0).gameObject);
                    }

                    if (callBackActionCell != null)
                    {
                        callBackActionCell(m_ItemToIndex + 1, moves[i].GetInstanceID(),
                            moves[i].GetChild(0).gameObject);
                    }

                    m_ArrayList.Add(moves[i]);
                }
                return true;
            }

            return false;
        }

        private bool ChangeRightToLeft()
        {
            moves.Clear();

            if (m_Direction == dirType.Horizontal)
            {
                if (m_ItemFromIndex <= 0)
                {
                    return false;
                }

                if ((m_ItemToIndex + 1) % m_ViewportItemRow != 0)
                {
                    int k = FindNearestMultiple(m_ItemToIndex + 1, m_ViewportItemRow) - m_ViewportItemRow;
                    k = m_ItemToIndex - (k - 1);
                    RemoveViewItems(m_ArrayList.Count - k, k, ref moves);
                }
                else
                {
                    RemoveViewItems(m_ArrayList.Count - m_ViewportItemRow, m_ViewportItemRow, ref moves);
                }

                if (moves == null || moves.Count != m_ViewportItemRow)
                {
                    return false;
                }

                for (int i = moves.Count - 1; i >= 0; i--)
                {
                    --m_ItemFromIndex;
                    --m_ItemToIndex;
                    moves[i].anchoredPosition3D = CalculateItemPos(m_ItemFromIndex);
                    if (callBackAction != null)
                    {
                        callBackAction(m_ItemFromIndex + 1, moves[i].GetChild(0).gameObject);
                    }

                    if (callBackActionCell != null)
                    {
                        callBackActionCell(m_ItemFromIndex + 1, moves[i].GetInstanceID(),
                            moves[i].GetChild(0).gameObject);
                    }
                }

                m_ArrayList.InsertRange(0, moves);

                return true;
            }

            return false;
        }

        private void RemoveViewItems(int start, int count, ref List<RectTransform> rects)
        {
            for (int i = start; i < start + count; ++i)
            {
                rects.Add(m_ArrayList[i]);
            }

            m_ArrayList.RemoveRange(start, count);
        }
        
        #endregion

        private int FindNearestMultiple(int a, int b)
        {
            return Mathf.CeilToInt((float)a / b) * b;
        }
    }
}
