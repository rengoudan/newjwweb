// 把服务端生成的文件交给浏览器下载（Blazor DotNetStreamReference）
window.jwDownload = async (fileName, streamRef) => {
    const buffer = await streamRef.arrayBuffer();
    const url = URL.createObjectURL(new Blob([buffer]));
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName ?? '';
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
};
