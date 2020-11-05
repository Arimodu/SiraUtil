using System;
using Zenject;
using IPA.Loader;
using ModestTree;
using System.Linq;
using SiraUtil.Events;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using IPA.Utilities;

namespace SiraUtil.Zenject
{
    internal class ZenjectManager
    {
        internal static bool ProjectContextWentOff { get; set; } = false;
        private readonly IDictionary<string, Zenjector> _allZenjectors = new Dictionary<string, Zenjector>();

        public ZenjectManager()
        {
            SiraEvents.ContextInstalling += SiraEvents_PreInstall;
            PluginManager.PluginEnabled += PluginManager_PluginEnabled;
            PluginManager.PluginDisabled += PluginManager_PluginDisabled;
        }

        private void PluginManager_PluginEnabled(PluginMetadata plugin, bool _)
        {
            if (_allZenjectors.TryGetValue(plugin.Id, out Zenjector zenjector) && zenjector.AutoControl)
            {
                zenjector.Enable();
            }
        }

        private void PluginManager_PluginDisabled(PluginMetadata plugin, bool _)
        {
            if (_allZenjectors.TryGetValue(plugin.Id, out Zenjector zenjector) && zenjector.AutoControl)
            {
                zenjector.Disable();
            }
        }

        internal void Add(Zenjector zenjector)
        {
            if (!_allZenjectors.ContainsKey(zenjector.Name))
            {
                _allZenjectors.Add(zenjector.Name, zenjector);
            }
        }

        private string ZenSource(InstallBuilder builder)
        {
            var zenjector = _allZenjectors.FirstOrDefault(x => x.Value.Builders.Contains(builder));
            return zenjector.Key;
        }

        #region Events

        private void SiraEvents_PreInstall(object sender, SiraEvents.SceneContextInstalledArgs e)
        {
            if (!ProjectContextWentOff)
            {
                if (e.Names.Contains("AppCore")) // AppCore is the first reported context.
                {
                    ProjectContextWentOff = true;
                }
                else
                {
                    return;
                }
            }
            var context = sender as Context;
            var builders = _allZenjectors.Values.Where(x => x.Enabled).SelectMany(x => x.Builders).Where(x => e.Names.Contains(x.Destination) && e.Names.ToList().Any(y => !x.Circuits.Contains(y)) && !x.Circuits.Contains(e.ModeInfo.Transition) && !x.Circuits.Contains(e.ModeInfo.Gamemode) && !x.Circuits.Contains(e.ModeInfo.MidScene)).ToList();
            var allInjectables = e.Decorators.SelectMany(x => Accessors.Injectables(ref x)).ToList();
            if (context is GameObjectContext gameObjectContext)
            {
                var monoList = new List<MonoBehaviour>();
                gameObjectContext.InvokeMethod<object, GameObjectContext>("GetInjectableMonoBehaviours", monoList);
                allInjectables.AddRange(monoList);
            }
            foreach (var builder in builders)
            {
                Plugin.Log.Sira($"Installing: {builder.Type.Name} ({ZenSource(builder)})");

                builder.Validate();
                foreach (var mutator in builder.Mutators)
                {
                    if (!allInjectables.Any(x => x.GetType() == mutator.Item1))
                    {
                        Assert.CreateException($"Could not find an object to mutate in a decorator context. {Utilities.ASSERTHIT}", mutator.Item1);
                    }
                    var behaviour = allInjectables.FirstOrDefault(x => x.GetType() == mutator.Item1);
                    if (behaviour != null)
                    {
                        var activeDecorator = e.Decorators.FirstOrDefault(x => Accessors.Injectables(ref x).Contains(behaviour));
                        mutator.Item2.Invoke(new MutationContext(e.Container, activeDecorator, allInjectables), behaviour);
                    }
                }
                foreach (var exposable in builder.Exposers)
                {
                    var behaviour = allInjectables.FirstOrDefault(x => x.GetType() == exposable);
                    if (behaviour != null)
                    {
                        if (!e.Container.HasBinding(behaviour.GetType()))
                        {
                            e.Container.Bind(exposable).FromInstance(behaviour).AsSingle();
                        }
                    }
                }

                if (builder.Parameters != null)
                {
                    var bases = context.NormalInstallers.ToList();
                    // Configurable Mono Installers requires the Unity Inspector
                    Assert.That(!builder.Type.DerivesFrom<MonoInstallerBase>(), $"MonoInstallers cannot have parameters due to Zenject limitations. {Utilities.ASSERTHIT}");
                    bases.Add(e.Container.Instantiate(builder.Type, builder.Parameters) as InstallerBase);
                    context.NormalInstallers = bases;

                    continue;
                }

                if (builder.Type.IsSubclassOf(typeof(MonoInstallerBase)))
                {
                    var monoInstallers = context.Installers.ToList();
                    monoInstallers.Add(context.gameObject.AddComponent(builder.Type) as MonoInstaller);
                    context.Installers = monoInstallers;

                    continue;
                }

                if (builder.Type.IsSubclassOf(typeof(InstallerBase)) && (builder.Parameters == null || builder.Parameters.Length == 0))
                {
                    context.AddNormalInstallerType(builder.Type);
                }
            }
        }

#endregion

        ~ZenjectManager()
        {
            SiraEvents.ContextInstalling -= SiraEvents_PreInstall;
            PluginManager.PluginEnabled -= PluginManager_PluginEnabled;
            PluginManager.PluginDisabled -= PluginManager_PluginDisabled;
        }
    }
}