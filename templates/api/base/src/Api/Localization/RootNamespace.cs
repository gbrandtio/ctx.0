using Microsoft.Extensions.Localization;

// The Api assembly is called "Api" but its root namespace is "CtxApp.Api", and
// resource lookup keys off the root namespace. Without this attribute the
// localizer would look for "Api.Resources.CtxApp.Api.Localization.Messages"
// while the build embeds "CtxApp.Api.Resources.Localization.Messages".
[assembly: RootNamespace("CtxApp.Api")]
