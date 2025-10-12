using Carter;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace MarineLitterMonitor.Server.WebServing;

public class TestModule : ICarterModule
{
    // 2. 实现 AddRoutes 方法，所有的路由都定义在这里
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // 3. 定义一个 GET 路由
        //    - 路径是 "/greet/{name}/{location}"
        //    - {name} 和 {location} 是占位符，代表 URL 参数
        app.MapGet("/test/{name}/{location}", (string name, string location) =>
        {
            // 4. 从 URL 自动绑定的参数，可以直接在 Lambda 中使用
            return $"你好, 来自 {location} 的 {name}! 欢迎使用 Carter! 喵~ ฅ'ω'ฅ";
        });
    }
}