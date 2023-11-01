using UnityEngine;

namespace SG
{
    /// <summary>
    /// 动态布局组件格子数据类
    /// </summary>
    public class DynamicGridLayoutCell : MonoBehaviour
    {
        private RectTransform RectTransform => transform as RectTransform;

        /// <summary>
        /// 格子变化Index值，位置改变时刷新
        /// </summary>
        [ReadOnlyInInspector] public int dynamicIndex;
        
        /// <summary>
        /// 列数
        /// </summary>
        public int column { get; private set; }

        /// <summary>
        /// 格子轴心的X坐标
        /// </summary>
        public float anchoredPositionX => RectTransform.anchoredPosition.x;

        /// <summary>
        /// 格子轴心的Y坐标
        /// </summary>
        public float anchoredPositionY => RectTransform.anchoredPosition.y;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="_index">           Index值 </param>
        /// <param name="_currRow">         Cell所在行数 </param>
        /// <param name="_currColumn">      Cell所在列数 </param>
        /// <param name="_cellWidth">       Cell的宽度 </param>
        /// <param name="_cellWidthSpace">  横向间隔 </param>
        /// <param name="_cellHeight">      Cell的高度 </param>
        /// <param name="_cellHeightSpace"> 纵向间隔 </param>
        public void Init(int _index, int _currRow, int _currColumn, float _cellWidth, float _cellWidthSpace, float _cellHeight, float _cellHeightSpace, Transform _transform)
        {
            dynamicIndex = _index;
            column = _currColumn;
            RectTransform.SetParent(_transform);
            
            // 将锚点设置为左上角对齐
            RectTransform.anchorMin = new Vector2(0, 1);
            RectTransform.anchorMax = new Vector2(0, 1);
            RectTransform.pivot = new Vector2(0, 1);
            transform.localScale = Vector3.one;
            RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _cellWidth);  // todo 这种控制尺寸方式应该有问题
            RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _cellHeight);

            float newAnchoredPositionX = _currColumn * (_cellWidth + _cellWidthSpace);
            float newAnchoredPositionY = -_currRow * (_cellHeight + _cellHeightSpace);
            RectTransform.anchoredPosition3D = new Vector3(newAnchoredPositionX, newAnchoredPositionY, 0);
        }

        /// <summary>
        /// 更新格子动态Index
        /// </summary>
        /// <param name="_index"></param>
        public void UpdateDynamicIndex(int _index) => dynamicIndex = _index;

        /// <summary>
        /// 更新格子的坐标
        /// </summary>
        public void UpdateAnchoredPosition(float _anchoredPositionX, float _anchoredPositionY)
        {
            RectTransform.anchoredPosition3D = new Vector3(_anchoredPositionX, _anchoredPositionY, 0);
        }
    }
}

