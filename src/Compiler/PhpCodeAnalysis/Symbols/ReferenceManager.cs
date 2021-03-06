﻿using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Utilities;
using System.Collections.Immutable;
using Pchp.CodeAnalysis.Symbols;
using System.Diagnostics;

namespace Pchp.CodeAnalysis
{
    partial class PhpCompilation
    {
        internal class ReferenceManager : CommonReferenceManager // TODO: inherit the generic version with all the Binding & resolving stuff
        {
            ImmutableArray<MetadataReference> _lazyExplicitReferences;
            ImmutableArray<MetadataReference> _lazyImplicitReferences = ImmutableArray<MetadataReference>.Empty;
            ImmutableDictionary<MetadataReference, IAssemblySymbol> _referencesMap;
            ImmutableDictionary<IAssemblySymbol, MetadataReference> _metadataMap;
            AssemblySymbol _lazyCorLibrary, _lazyPhpCorLibrary;

            readonly Dictionary<AssemblyIdentity, PEAssemblySymbol> _assembliesMap = new Dictionary<AssemblyIdentity, PEAssemblySymbol>();

            /// <summary>
            /// COR library containing base system types.
            /// </summary>
            internal AssemblySymbol CorLibrary => _lazyCorLibrary;

            /// <summary>
            /// PHP COR library containing PHP runtime.
            /// </summary>
            internal AssemblySymbol PhpCorLibrary => _lazyPhpCorLibrary;

            internal override ImmutableArray<MetadataReference> ExplicitReferences => _lazyExplicitReferences;

            internal override ImmutableArray<MetadataReference> ImplicitReferences => _lazyImplicitReferences;

            internal override IEnumerable<KeyValuePair<AssemblyIdentity, PortableExecutableReference>> GetImplicitlyResolvedAssemblyReferences()
            {
                foreach (var pair in _metadataMap)
                {
                    var per = pair.Value as PortableExecutableReference;
                    if (per != null)
                    {
                        yield return new KeyValuePair<AssemblyIdentity, PortableExecutableReference>(pair.Key.Identity, per);
                    }
                }
            }

            internal override MetadataReference GetMetadataReference(IAssemblySymbol assemblySymbol) => _metadataMap.TryGetOrDefault(assemblySymbol);

            internal override IEnumerable<KeyValuePair<MetadataReference, IAssemblySymbol>> GetReferencedAssemblies() => _referencesMap;

            internal override IEnumerable<ValueTuple<IAssemblySymbol, ImmutableArray<string>>> GetReferencedAssemblyAliases()
            {
                yield break;
            }

            IEnumerable<string> CorLibReferences
            {
                get
                {
                    // mscorlib
                    yield return @"mscorlib";

                    // pchpcor
                    yield return @"pchpcor, Version=1.0.0.0, Culture=neutral, PublicKeyToken=5b4bee2bf1f98593";
                }
            }

            PEAssemblySymbol CreateAssemblyFromIdentity(MetadataReferenceResolver resolver, AssemblyIdentity identity, string basepath, List<PEModuleSymbol> modules)
            {
                PEAssemblySymbol ass;
                if (!_assembliesMap.TryGetValue(identity, out ass))
                {
                    var pe = resolver.ResolveReference(identity.GetDisplayName(), basepath, new MetadataReferenceProperties())[0];
                    
                    _assembliesMap[identity] = ass = PEAssemblySymbol.Create(pe);
                    ass.SetCorLibrary(_lazyCorLibrary);
                    modules.AddRange(ass.Modules.Cast<PEModuleSymbol>());
                }

                return ass;
            }

            void SetReferencesOfReferencedModules(MetadataReferenceResolver resolver, List<PEModuleSymbol> modules)
            {
                for (int i = 0; i < modules.Count; i++)
                {
                    var refs = modules[i].Module.ReferencedAssemblies;
                    var symbols = new AssemblySymbol[refs.Length];
                    
                    for (int j = 0; j < refs.Length; j++)
                    {
                        var symbol = CreateAssemblyFromIdentity(resolver, refs[j], null, modules);
                        symbols[j] = symbol;
                    }

                    //
                    modules[i].SetReferences(new ModuleReferences<AssemblySymbol>(refs, symbols.AsImmutable(), ImmutableArray<UnifiedAssembly<AssemblySymbol>>.Empty));
                }
            }

            internal void CreateSourceAssemblyForCompilation(PhpCompilation compilation)
            {
                if (compilation._lazyAssemblySymbol != null)
                    return;

                // TODO: lock

                Debug.Assert(_lazyExplicitReferences.IsDefault);
                Debug.Assert(_lazyCorLibrary == null);
                Debug.Assert(_lazyPhpCorLibrary == null);

                //
                var externalRefs = CorLibReferences.SelectMany(reference => compilation.Options.MetadataReferenceResolver.ResolveReference(reference, null, new MetadataReferenceProperties()))
                    .Concat(compilation.ExternalReferences).AsImmutable();
                var assemblies = new List<AssemblySymbol>(externalRefs.Length);

                var referencesMap = new Dictionary<MetadataReference, IAssemblySymbol>();
                var metadataMap = new Dictionary<IAssemblySymbol, MetadataReference>();
                var assembliesMap = new Dictionary<AssemblyIdentity, PEAssemblySymbol>();

                var refmodules = new List<PEModuleSymbol>();
                
                foreach (PortableExecutableReference pe in externalRefs)
                {
                    var symbol = PEAssemblySymbol.Create(pe);
                    if (symbol != null)
                    {
                        assemblies.Add(symbol);
                        referencesMap[pe] = symbol;
                        metadataMap[symbol] = pe;

                        if (_lazyCorLibrary == null && symbol.IsCorLibrary)
                            _lazyCorLibrary = symbol;

                        if (_lazyPhpCorLibrary == null && symbol.IsPchpCorLibrary)
                            _lazyPhpCorLibrary = symbol;

                        // cache bound assembly symbol
                        _assembliesMap.Add(symbol.Identity, symbol);

                        // list of modules to initialize later
                        refmodules.AddRange(symbol.Modules.Cast<PEModuleSymbol>());
                    }
                }

                //
                _lazyExplicitReferences = externalRefs;
                _lazyImplicitReferences = ImmutableArray<MetadataReference>.Empty;
                _metadataMap = metadataMap.ToImmutableDictionary();
                _referencesMap = referencesMap.ToImmutableDictionary();

                //
                var assembly = new SourceAssemblySymbol(compilation, compilation.Options.ModuleName, compilation.Options.ModuleName);
                assembly.SetCorLibrary(_lazyCorLibrary);
                compilation._lazyAssemblySymbol = assembly;

                assembly.SourceModule.SetReferences(new ModuleReferences<AssemblySymbol>(
                    assemblies.Select(x => x.Identity).AsImmutable(),
                    assemblies.AsImmutable(),
                    ImmutableArray<UnifiedAssembly<AssemblySymbol>>.Empty), assembly);

                assemblies.ForEach(ass => ass.SetCorLibrary(_lazyCorLibrary));

                // recursively initialize references of referenced modules
                SetReferencesOfReferencedModules(compilation.Options.MetadataReferenceResolver, refmodules);
            }
        }
    }
}
