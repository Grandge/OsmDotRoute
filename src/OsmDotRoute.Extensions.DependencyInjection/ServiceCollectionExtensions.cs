using Microsoft.Extensions.DependencyInjection;
using OsmDotRoute.Itinero;

namespace OsmDotRoute.Extensions.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> に OsmDotRoute 一式を登録するための拡張メソッド。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Itinero RouterDb ファイルパスを指定して OsmDotRoute サービス一式を Singleton で登録する。
    /// 登録される型: <see cref="RouterDb"/>、<see cref="Router"/>、<see cref="RestrictedAreaService"/>。
    /// </summary>
    /// <param name="services">対象のサービスコレクション</param>
    /// <param name="routerDbPath">起動時に読み込む Itinero RouterDb ファイルのパス</param>
    /// <returns>メソッドチェーン用 <paramref name="services"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> が <c>null</c></exception>
    /// <exception cref="ArgumentException"><paramref name="routerDbPath"/> が <c>null</c> または空白</exception>
    public static IServiceCollection AddOsmDotRoute(this IServiceCollection services, string routerDbPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(routerDbPath);

        return AddOsmDotRoute(services, options => options.RouterDbPath = routerDbPath);
    }

    /// <summary>
    /// <see cref="OsmDotRouteOptions"/> 経由で詳細設定を行いながら OsmDotRoute サービス一式を Singleton で登録する。
    /// 登録される型: <see cref="RouterDb"/>、<see cref="Router"/>、<see cref="RestrictedAreaService"/>。
    /// </summary>
    /// <param name="services">対象のサービスコレクション</param>
    /// <param name="configure">オプション構築デリゲート</param>
    /// <returns>メソッドチェーン用 <paramref name="services"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> または <paramref name="configure"/> が <c>null</c></exception>
    /// <exception cref="InvalidOperationException"><see cref="OsmDotRouteOptions.RouterDbPath"/> が未設定</exception>
    public static IServiceCollection AddOsmDotRoute(this IServiceCollection services, Action<OsmDotRouteOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new OsmDotRouteOptions();
        configure(options);
        if (string.IsNullOrWhiteSpace(options.RouterDbPath))
        {
            throw new InvalidOperationException($"{nameof(OsmDotRouteOptions)}.{nameof(OsmDotRouteOptions.RouterDbPath)} を設定してください。");
        }

        services.AddSingleton(_ => ItineroRouterDbLoader.LoadFromFile(options.RouterDbPath!));
        services.AddSingleton<RestrictedAreaService>();
        services.AddSingleton(sp => new Router(sp.GetRequiredService<RouterDb>(), sp.GetRequiredService<RestrictedAreaService>()));

        return services;
    }
}
