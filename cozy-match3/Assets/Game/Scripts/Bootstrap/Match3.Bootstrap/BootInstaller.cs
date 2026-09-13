using Match3.Core;
using Match3.Diagnostics;
using Zenject;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Boot scene bindings. Deliberately minimal: the loading screen only needs somewhere to
    /// report a failed load.
    /// </summary>
    public sealed class BootInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<IMatch3Logger>().To<UnityMatch3Logger>().AsSingle();
        }
    }
}
