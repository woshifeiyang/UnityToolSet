using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SG
{
    public class DynamicGridLayoutVertical : MonoBehaviour
    {
        /// <summary>
        /// 格子排列方式
        /// </summary>
        public enum ChildAlignment
        {
            /// <summary>
            /// 左上角对齐
            /// </summary>
            UpperLeft = 0,

            /// <summary>
            /// 上居中对齐
            /// </summary>
            UpperCenter = 1,

            /// <summary>
            /// 右上角对齐
            /// </summary>
            UpperRight = 2
        }

        #region Attributes and Properties

        /// <summary>
        /// 占位格子大小
        /// </summary>
        [SerializeField] private Vector2 m_cell;

        public float cellWidth => Mathf.Max(0, m_cell.x * m_localScale.x);

        public float cellHeight => Mathf.Max(0, m_cell.y * m_localScale.y);

        /// <summary>
        /// 占位格子间距
        /// </summary>
        [SerializeField] private Vector2 m_space;

        public float cellWidthSpace => Mathf.Max(0, m_space.x * m_localScale.x);

        public float cellHeightSpace => Mathf.Max(0, m_space.y * m_localScale.y);

        /// <summary>
        /// 子对象排列方式
        /// </summary>
        [SerializeField] private ChildAlignment m_childAlignment = ChildAlignment.UpperLeft;

        /// <summary>
        /// ScrollRect组件
        /// </summary>
        private ScrollRect m_scrollRect;

        /// <summary>
        /// RectTransform组件
        /// </summary>
        private RectTransform m_rectTransform => transform as RectTransform;

        /// <summary>
        /// 格子预制体
        /// </summary>
        private GameObject m_cellPrefab;

        /// <summary>
        /// 格子内容更新Action
        /// </summary>
        private Action<GameObject, int> m_cellContentUpdateAction;

        /// <summary>
        /// 双向链表
        /// </summary>
        private LinkedList<GameObject> m_cellLinkedList;

        /// <summary>
        /// Viewport长度和宽度 用于计算占位格子行列数
        /// </summary>
        private float m_viewportWidth;

        private float m_viewportHeight;

        /// <summary>
        /// Content初始缩放
        /// </summary>
        private Vector3 m_localScale;

        /// <summary>
        /// 占位格子行列数
        /// </summary>
        private int m_row;
        
        private int m_column;

        /// <summary>
        /// 需要显示对象的总数
        /// </summary>
        private int m_totalCellNum;

        /// <summary>
        /// 上一帧位置 用于计算滚动方向
        /// </summary>
        private Vector2 m_prevPosition;

        #endregion

        #region Engine Methods

#if UNITY_EDITOR
        protected void OnValidate()
        {
            // todo 动态查看布局变化
            // Debug.Log("监听布局变化");
        }

#endif

        #endregion

        #region Public Methods

        /// <summary>
        /// 初始化布局
        /// </summary>
        public void Init()
        {
            m_scrollRect = GetComponentInParent<ScrollRect>(true);

            if (m_scrollRect == null)
            {
                Debug.LogError("Can not find ScrollRect component in parent  " + gameObject.name);
                return;
            }

            // ScrollRect添加事件监听
            m_scrollRect.onValueChanged.AddListener(UpdateCellPosition);

            // 初始化视图长宽,缩放
            Rect viewportRect = m_scrollRect.viewport.GetComponent<RectTransform>().rect;
            m_viewportWidth = viewportRect.width;
            m_viewportHeight = viewportRect.height;

            m_localScale = transform.localScale;
            m_prevPosition = m_rectTransform.anchoredPosition;
            
            m_cellLinkedList = new LinkedList<GameObject>();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Exit()
        {
            if (m_scrollRect == null)
            {
                Debug.LogError("ScrollRect reference is null");
                return;
            }

            // ScrollRect移除事件监听
            m_scrollRect.onValueChanged.RemoveListener(UpdateCellPosition);

            // 释放格子对象内存
            if (m_cellLinkedList is { Count: > 0 })
            {
                foreach (var cell in m_cellLinkedList)
                {
                    Destroy(cell);
                }
            }
                
            m_cellLinkedList.Clear();
        }

        /// <summary>
        /// 注册信息
        /// </summary>
        /// <param name="_cellPrefab">预制体对象</param>
        /// <param name="_action">预制体更新内容Action</param>
        /// <param name="_totalNum">需要显示对象的总数</param>
        public void RegisterAttribute(GameObject _cellPrefab, Action<GameObject, int> _action, int _totalNum)
        {
            m_cellPrefab = _cellPrefab;
            m_cellContentUpdateAction = _action;
            m_totalCellNum = Mathf.Max(0, _totalNum);

            // 初始化Content宽高与Viewport一致
            m_rectTransform.anchorMin = Vector2.zero;
            m_rectTransform.anchorMax = Vector2.one;
            m_rectTransform.offsetMax = new Vector2(0, 0);
            m_rectTransform.offsetMin = new Vector2(0, 0);
            
            // 设置行列数和Content宽高
            m_row = GetRowNumber();
            m_column = GetColumnNumber();
            SetContentTransform();
        }

        /// <summary>
        /// 生成占位格子并初始化
        /// </summary>
        public void SpawnCellOnPosition()
        {
            if (m_totalCellNum > 0)
            {
                // 清除原有数据
                if (m_cellLinkedList is { Count: > 0 })
                {
                    foreach (var cell in m_cellLinkedList)
                    {
                        Destroy(cell);
                    }
                }
                
                m_cellLinkedList.Clear();
                
                for (int currRow = 0; currRow < m_row; ++currRow)
                {
                    for (int currColumn = 0; currColumn < m_column; ++currColumn)
                    {
                        int index = currRow * m_column + currColumn;
                        if(index > m_totalCellNum - 1) continue;        // 防止越界

                        // 实例化对象
                        GameObject go = Instantiate(m_cellPrefab);
                        go.SetActive(true);
                        m_cellLinkedList.AddLast(go);
                        
                        // 初始化格子数据类
                        DynamicGridLayoutCell cellInfo = go.AddComponent<DynamicGridLayoutCell>();
                        cellInfo.Init(index, currRow, currColumn, cellWidth, cellWidthSpace, cellHeight, cellHeightSpace, transform);
                        
                        // 通过Index调用格子刷新事件
                        m_cellContentUpdateAction?.Invoke(go, index);
                    }
                }
            }
        }

        /// <summary>
        /// 移动占位格子至目标位置
        /// </summary>
        /// <param name="_index">占位格唯一编号</param>
        public void MoveToTargetIndex(int _index)
        {
            if (m_cellLinkedList is { Count: > 0 })
            {
                if (_index < 0 || _index % m_column != 0)
                {
                    return;
                }
                
                int index = _index + m_row <= m_totalCellNum? _index : m_totalCellNum - m_row;
                
                // 移动Content
                int targetRow = index / m_column;
                float targetHeight = targetRow * (cellHeight + cellHeightSpace);
                m_rectTransform.anchoredPosition3D = new Vector3(0, targetHeight, 0);

                // 刷新占位格子位置和内容
                foreach (var cell in m_cellLinkedList)
                {
                    if (cell.TryGetComponent(out DynamicGridLayoutCell cellInfo))
                    {
                        if(index > m_totalCellNum - 1) continue;        // 防止越界

                        cellInfo.UpdateDynamicIndex(index);
                        cellInfo.UpdateAnchoredPosition(cellInfo.anchoredPositionX , -targetHeight);
                        m_cellContentUpdateAction?.Invoke(cell, index);

                        ++index;
                        targetHeight += cellHeight + cellHeightSpace;
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 拖动滚动条时实时更新格子位置
        /// </summary>
        /// <param name="pos"></param>
        private void UpdateCellPosition(Vector2 pos)
        {
            if (m_totalCellNum == 0 || m_cellLinkedList.Count == 0) return;

            // 滚动条向下拖动
            if (pos.y - m_prevPosition.y < 0)
            {
                RectTransform firstCellRectTransform = m_cellLinkedList.First.Value.GetComponent<RectTransform>();
                float offset = firstCellRectTransform.anchoredPosition.y + m_rectTransform.offsetMax.y; // 格子Pivot相对于Viewport上边界的偏移量
                float topBoundary = cellHeight;
                
                // 格子超过Viewport上边界时，移动格子至最后一行
                if (offset > topBoundary)
                {
                    for (int i = 0; i < m_column; ++i)
                    {
                        GameObject cell = m_cellLinkedList.First.Value;
                        if (cell.TryGetComponent(out DynamicGridLayoutCell cellAttribute))
                        {
                            float newPositionY = cellAttribute.anchoredPositionY - m_row * (cellHeight + cellHeightSpace);
                            
                            // 未到底部时移动格子
                            if (newPositionY > -m_rectTransform.rect.height)
                            {
                                // 更新Index和位置
                                int index = cellAttribute.dynamicIndex + m_row * m_column;
                                cellAttribute.UpdateDynamicIndex(index);
                                cellAttribute.UpdateAnchoredPosition(cellAttribute.anchoredPositionX, newPositionY);
                            
                                // 执行Action
                                if(index >= m_totalCellNum || index < 0) cell.SetActive_Self(false);
                                else
                                {
                                    cell.SetActive_Self(true);
                                    m_cellContentUpdateAction?.Invoke(cell, index);
                                }
                                
                                // 移动节点到双向链表尾部
                                m_cellLinkedList.AddLast(cell);
                                m_cellLinkedList.RemoveFirst();
                            }
                        }
                    }
                }
            }
            // 滚动条向上拖动
            else if (pos.y - m_prevPosition.y > 0)
            {
                RectTransform firstCellRectTransform = m_cellLinkedList.Last.Value.GetComponent<RectTransform>();
                float offset = firstCellRectTransform.anchoredPosition.y + m_rectTransform.offsetMax.y; // 格子Pivot相对于Viewport上边界的偏移量
                float bottomBoundary = -(m_row - 1) * (cellHeight + cellHeightSpace);
                
                // 格子超过Viewport下边界可视范围时，移动格子至第一行
                if (offset < bottomBoundary)
                {
                    for (int i = 0; i < m_column; ++i)
                    {
                        GameObject cell = m_cellLinkedList.Last.Value;
                        if (cell.TryGetComponent(out DynamicGridLayoutCell cellAttribute))
                        {
                            float newPositionY = cellAttribute.anchoredPositionY + m_row * (cellHeight + cellHeightSpace);
                            if (newPositionY <= 0)
                            {
                                // 更新Index
                                int index = cellAttribute.dynamicIndex - m_row * m_column;
                                cellAttribute.UpdateDynamicIndex(index);
                            
                                // 更新位置
                                cellAttribute.UpdateAnchoredPosition(cellAttribute.anchoredPositionX, newPositionY);

                                // 执行Action
                                if(index >= m_totalCellNum || index < 0) cell.SetActive_Self(false);
                                else
                                {
                                    cell.SetActive_Self(true);
                                    m_cellContentUpdateAction?.Invoke(cell, index);
                                }
                                
                                // 移动节点到双向链表首部
                                m_cellLinkedList.AddFirst(cell);
                                m_cellLinkedList.RemoveLast();
                            }
                        }
                    }
                }
            }
            
            m_prevPosition = pos;
        }
        
        /// <summary>
        /// 设置Content大小
        /// </summary>
        private void SetContentTransform()
        {
            float contentHeight = (cellHeight + cellHeightSpace) * Mathf.CeilToInt((float)m_totalCellNum / m_column) - cellHeightSpace;
            contentHeight = contentHeight > m_viewportHeight ? contentHeight : m_viewportHeight + 10;       // 高度和viewport完全一致的话拖拽时会闪动

            m_rectTransform.localScale = Vector3.one;
            m_rectTransform.pivot = new Vector2(0, 1);
            m_rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, m_viewportWidth);
            m_rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        }

        /// <summary>
        /// Viewport可显示最大列数
        /// </summary>
        private int GetColumnNumber()
        {
            int columnNum = 0;
            float currWidth = 0;

            while (currWidth + cellWidth + cellWidthSpace < m_viewportWidth)
            {
                columnNum++;
                currWidth += cellWidth + cellWidthSpace;
            }

            if (currWidth + cellWidth <= m_viewportWidth) columnNum++;
            
            return Mathf.Max(1, columnNum);
        }

        /// <summary>
        /// Viewport可显示最大行数
        /// </summary>
        private int GetRowNumber()
        {
            int rowNum = 0;
            float currHeight = 0;

            while (currHeight + cellHeight + cellHeightSpace < m_viewportHeight)
            {
                rowNum++;
                currHeight += cellHeight + cellHeightSpace;
            }

            if (currHeight + cellHeight <= m_viewportHeight) rowNum++;
            
            return Mathf.Max(1, rowNum + 1);    // 为了滚动效果需要多增加一行
        }

        #endregion
    }
}