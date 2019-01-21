﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pchp.CodeAnalysis.Errors
{
    /// <summary>
    /// A database of all possible diagnostics used by PHP compiler. The severity can be determined by the prefix:
    /// "FTL_" stands for fatal error, "ERR_" for error, "WRN_" for warning, "INF_" for visible information and
    /// "HDN_" for hidden information. Messages and other information are stored in the resources,
    /// <see cref="ErrorFacts"/> contains the naming logic.
    /// </summary>
    /// <remarks>
    /// New diagnostics must be added to the end of the corresponding severity group in order not to change the
    /// codes of the current ones.
    /// </remarks>
    internal enum ErrorCode
    {
        // 0xxx: reserved
        // 1xxx: reserved
        // 2xxx: reserved

        // 
        // Fatal errors
        //
        FTL_InvalidInputFileName = 3000,

        //
        // Errors
        //
        ERR_BadCompilationOptionValue = 4000,
        ERR_BadWin32Resource,
        ERR_BinaryFile,
        ERR_CantOpenFileWrite,
        ERR_CantOpenWin32Icon,
        ERR_CantOpenWin32Manifest,
        ERR_CantOpenWin32Resource,
        ERR_CantReadResource,
        ERR_CantReadRulesetFile,
        ERR_CompileCancelled,
        ERR_EncReferenceToAddedMember,
        ERR_ErrorBuildingWin32Resource,
        ERR_ErrorOpeningAssemblyFile,
        ERR_ErrorOpeningModuleFile,
        ERR_ExpectedSingleScript,
        ERR_FailedToCreateTempFile,
        ERR_FileNotFound,
        ERR_InvalidAssemblyMetadata,
        ERR_InvalidDebugInformationFormat,
        ERR_MetadataFileNotAssembly,
        ERR_InvalidFileAlignment,
        ERR_InvalidModuleMetadata,
        ERR_InvalidOutputName,
        ERR_InvalidPathMap,
        ERR_InvalidSubsystemVersion,
        ERR_LinkedNetmoduleMetadataMustProvideFullPEImage,
        ERR_MetadataFileNotFound,
        ERR_MetadataFileNotModule,
        ERR_MetadataNameTooLong,
        ERR_MetadataReferencesNotSupported,
        ERR_NoSourceFile,
        ERR_StartupObjectNotFound,
        ERR_OpenResponseFile,
        ERR_OutputWriteFailed,
        ERR_PdbWritingFailed,
        ERR_PermissionSetAttributeFileReadError,
        ERR_PublicKeyContainerFailure,
        ERR_PublicKeyFileFailure,
        ERR_ResourceFileNameNotUnique,
        ERR_ResourceInModule,
        ERR_ResourceNotUnique,
        ERR_TooManyUserStrings,
        ERR_NotYetImplemented, // Used for all valid PHP constructs that Peachipe doesn't currently support.
        ERR_CircularBase,
        ERR_TypeNameCannotBeResolved,
        ERR_PositionalArgAfterUnpacking,    // Cannot use positional argument after argument unpacking
        /// <summary>Call to a member function {0} on {1}</summary>
        ERR_MethodCalledOnNonObject,
        /// <summary>Value of type {0} cannot be passed by reference</summary>
        ERR_ValueOfTypeCannotBeAliased,
        /// <summary>"Cannot instantiate {0} {1}", e.g. "interface", the type name</summary>
        ERR_CannotInstantiateType,
        /// <summary>"{0} cannot use {1} - it is not a trait"</summary>
        ERR_CannotUseNonTrait,
        /// <summary>"Class {0} cannot extend from {1} {2}", e.g. from trait T</summary>
        ERR_CannotExtendFrom,
        /// <summary>"{0} cannot implement {1} - it is not an interface"</summary>
        ERR_CannotImplementNonInterface,
        /// <summary>Method {0}::__toString() must return a string value</summary>
        ERR_ToStringMustReturnString,
        /// <summary>{0}() cannot declare a return type</summary>
        ERR_CannotDeclareReturnType,
        /// <summary>A void function must not return a value</summary>
        ERR_VoidFunctionCannotReturnValue,
        /// <summary>{0} {1}() must take exactly {2} arguments</summary>
        ERR_MustTakeArgs,
        /// <summary>Function name must be a string, {0} given</summary>
        ERR_InvalidFunctionName,
        /// <summary>Cannot use the final modifier on an abstract class</summary>
        ERR_FinalAbstractClassDeclared,
        /// <summary>Access level to {0}::${1} must be {2} (as in class {3}) or weaker</summary>
        ERR_PropertyAccessibilityError,
        /// <summary>Use of primitive type '{0}' is misused</summary>
        ERR_PrimitiveTypeNameMisused,
        /// <summary>Missing value for '{0}' option</summary>
        ERR_SwitchNeedsValue,
        /// <summary>'{0}' not in the 'loop' or 'switch' context</summary>
        ERR_NeedsLoopOrSwitch,
        /// <summary>Provided source code kind is unsupported or invalid: '{0}'</summary>
        ERR_BadSourceCodeKind,
        /// <summary>Provided documentation mode is unsupported or invalid: '{0}'.</summary>
        ERR_BadDocumentationMode,
        /// <summary>Compilation options '{0}' and '{1}' can't both be specified at the same time.</summary>
        ERR_MutuallyExclusiveOptions,
        /// <summary>Invalid instrumentation kind: {0}</summary>
        ERR_InvalidInstrumentationKind,
        /// <summary>Invalid hash algorithm name: '{0}'</summary>
        ERR_InvalidHashAlgorithmName,
        /// <summary>Option '{0}' must be an absolute path.</summary>
        ERR_OptionMustBeAbsolutePath,
        /// <summary>Cannot emit debug information for a source text without encoding.</summary>
        ERR_EncodinglessSyntaxTree,
        /// <summary>An error occurred while writing the output file: {0}.</summary>
        ERR_PeWritingFailure,
        /// <summary>Failed to emit module '{0}'.</summary>
        ERR_ModuleEmitFailure,
        /// <summary>Cannot update '{0}'; attribute '{1}' is missing.</summary>
        ERR_EncUpdateFailedMissingAttribute,
        /// <summary>Unable to read debug information of method '{0}' (token 0x{1:X8}) from assembly '{2}'</summary>
        ERR_InvalidDebugInfo,
        /// <summary>Invalid assembly name: {0}</summary>
        ERR_BadAssemblyName,
        /// <summary>/embed switch is only supported when emitting Portable PDB (/debug:portable or /debug:embedded).</summary>
        ERR_CannotEmbedWithoutPdb,
        /// <summary>No overload for method {0} can be called.</summary>
        ERR_NoMatchingOverload,
        //
        // Warnings
        //
        WRN_AnalyzerCannotBeCreated = 5000,
        WRN_NoAnalyzerInAssembly,
        WRN_NoConfigNotOnCommandLine,
        WRN_PdbLocalNameTooLong,
        WRN_PdbUsingNameTooLong,
        WRN_UnableToLoadAnalyzer,
        WRN_UndefinedFunctionCall,
        WRN_UninitializedVariableUse,
        WRN_UndefinedType,
        WRN_UndefinedMethodCall,
        /// <summary>The declaration of class, interface or trait is ambiguous since its base types cannot be resolved.</summary>
        WRN_AmbiguousDeclaration,
        WRN_UnreachableCode,
        WRN_NotYetImplementedIgnored,
        WRN_NoSourceFiles,
        /// <summary>{0}() expects {1} parameter(s), {2} given</summary>
        WRN_TooManyArguments,
        /// <summary>{0}() expects at least {1} parameter(s), {2} given</summary>
        WRN_MissingArguments,
        /// <summary>Assertion will always fail</summary>
        WRN_AssertAlwaysFail,
        /// <summary>Using string as the assertion is deprecated</summary>
        WRN_StringAssertionDeprecated,
        /// <summary>Deprecated: {0} '{1}' has been deprecated. {2}</summary>
        WRN_SymbolDeprecated,
        /// <summary>The expression is not being read. Did you mean to assign it somewhere?</summary>
        WRN_ExpressionNotRead,
        /// <summary>Assignment made to same variable; did you mean to assign something else?</summary>
        WRN_AssigningSameVariable,
        

        //
        // Visible information
        //
        INF_UnableToLoadSomeTypesInAnalyzer = 6000,
        INF_EvalDiscouraged,
    }
}
