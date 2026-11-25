using JwShapeCommon.Compat; // 旧版 using Sunny.UI 带来的 First()/Last() 语义，见 SunnyCollectionsCompat
using JwCore;
using JwShapeCommon.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JwShapeCommon
{
    public class GlobalEvent
    {
        // 移植改造：旧版是进程级单例（GetGlobalEvent），所有解析共享、订阅从不取消。
        // 现在每张图纸一个实例（JwDrawing.Events），事件只在本图纸内流动。


        public EventHandler<ChangePageArgs> ChangeJwPage;

        public EventHandler<SetNewPageArgs> SetNewPages;


        public EventHandler<ShowParseLogArgs> ShowParseLogEvent;

        public EventHandler<ApiErrorArgs> ApiErrorEvent;

        public EventHandler LoginUserLoadEvent;

        public EventHandler<AddLinkPartArgs> AddLinkPartEvent;

        public EventHandler<ControlSelectedSquareArgs> ControlSelectedSquareEvent;

        public EventHandler<ControlSelectedSquareArgs> DeleteSelectedSquareEvent;

        /// <summary>
        /// 操作记录日志
        /// </summary>
        public EventHandler<OperateLogArgs> OperateLogEvent;

        public AsyncEvent<UpdateCodeArgs> UpdateCodeEvent { get; } = new();

        //public EventHandler<UpdateCodeArgs> UpdateCodeEvent;

        public EventHandler<UpdateCodeArgs> UpdateNewGongQuEvent;

        public EventHandler<DrawAuxiliaryLineArgs> DrawAuxiliaryLineEvent;

        public EventHandler<WarningArgs> WarningEvent;

        public EventHandler RefreshDataEvent;

        public EventHandler RefreshShowBeamsEvent;
    }

    /// <summary>
    /// 全局warning事件参数
    /// </summary>
    public class WarningArgs : EventArgs
    {
        public string WarningMsg { get; set; }
    }

    public  class DrawAuxiliaryLineArgs : EventArgs
    {
        public double Auxiliary { get; set; }

        public BeamDirectionType DirectionType { get; set; }

    }

    /// <summary>
    /// 更新梁工区
    /// </summary>
    public class UpdateCodeArgs: EventArgs
    {
        public string Id { get; set; }

        /// <summary>
        /// 用作工区
        /// </summary>
        public string NewCode { get; set; }
        public DrawShapeType DrawShapeType { get; set; }
    }

    /// <summary>
    /// 暂时无用
    /// </summary>
    public class DeletePartArgs : EventArgs
    {
        public string DeleteId { get; set; }

        public DrawShapeType DrawShapeType { get; set; }
    }


    public class ApiErrorArgs : EventArgs
    {
        public object Error { get; set; }
    }

    public class ChangePageArgs : EventArgs
    {
        public bool ChangeHead { get; set; }
        public int PageId { get; set; }

        public object JwProject { get; set; }
    }

    public class SetNewPageArgs : EventArgs
    {
        public object NewPage { get; set; }

        public int PageId { get; set; }

    }


    public class ShowParseLogArgs : EventArgs
    {
        public string Msg { get; set; }

        public DateTime? UpdateTime { get; set; }

        public bool ShowTime { get; set; }
    }

    public class AddLinkPartArgs : EventArgs
    {
        public JwLinkPart LinkPart { get; set; }
    }

    /// <summary>
    /// 删除指定形状
    /// </summary>
    public class ControlSelectedSquareArgs : EventArgs
    {
        public string Id { get; set; }

        public DrawShapeType DrawShapeType { get; set; }

        /// <summary>
        /// 传入subid 用来重新计算删除后的数量
        /// </summary>
        public string SubId { get; set; }

        public bool IsLianjie { get; set; }
    }

    public class OperateLogArgs : EventArgs
    {
        public JwOperateLogData JwOperateLogData { get; set; }
    }
}
