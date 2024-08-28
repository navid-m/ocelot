using DotNetty.Codecs.Http;
using DotNetty.Transport.Channels;
using Ocelot.Reports;

namespace Ocelot.Server.Internal;

internal sealed class AppServerHandler(App app) : SimpleChannelInboundHandler<IFullHttpRequest>
{
    private readonly App _app = app;

    protected override async void ChannelRead0(IChannelHandlerContext ctx, IFullHttpRequest msg)
    {
        await _app.HandleRequestAsync(ctx, msg);
    }

    public override void ExceptionCaught(IChannelHandlerContext ctx, Exception exception)
    {
        Logger.LogIssue(
            $"Error processing request: {exception.Message}\nTrace: {exception.StackTrace}"
        );
        ctx.CloseAsync();
    }
}
