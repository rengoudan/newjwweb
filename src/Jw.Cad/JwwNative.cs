namespace JwShapeCommon
{
    /// <summary>
    /// 原生 JwwHelper（MFC 的 C++/CLI 组件）不是线程安全的：两个线程同时读写 .jww 会在原生代码里崩溃（SEHException）。
    /// 所有对 JwwHelper 的读、写调用都要在这个锁内执行；读完之后的识别步骤是纯托管代码，可以并行。
    /// 这是对外部组件的串行化保护，不保存任何业务状态。
    /// </summary>
    public static class JwwNative
    {
        public static readonly object Sync = new();
    }
}
