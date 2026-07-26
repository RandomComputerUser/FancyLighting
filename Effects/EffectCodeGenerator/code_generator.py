import json
import unicodedata
from dataclasses import dataclass
from enum import auto, Flag


class EffectFlag:
    def __init__(self, name: str, disabled_name: str | None = None) -> None:
        self._name: str = unicodedata.normalize("NFC", name.strip())
        self._disabled_name: str | None = (
            unicodedata.normalize("NFC", disabled_name.strip())
            if isinstance(disabled_name, str)
            else None
        )

    def name(self, enabled: bool) -> str:
        if enabled:
            return self._name
        else:
            return "" if self._disabled_name is None else self._disabled_name

    def __hash__(self) -> int:
        return hash((self._name, self._disabled_name))

    def __eq__(self, other: object) -> bool:
        return (
            isinstance(other, EffectFlag)
            and self._name == other._name
            and self._disabled_name == other._disabled_name
        )


class EffectFeatures(Flag):
    NONE = auto()
    LIGHT_ONLY = auto()
    HI_DEF = auto()
    LIGHT_ONLY_HI_DEF = auto()

    @staticmethod
    def LightOnly() -> "EffectFeatures":
        return EffectFeatures.NONE | EffectFeatures.LIGHT_ONLY

    @staticmethod
    def HiDef() -> "EffectFeatures":
        return EffectFeatures.NONE | EffectFeatures.HI_DEF

    @staticmethod
    def All() -> "EffectFeatures":
        return (
            EffectFeatures.NONE
            | EffectFeatures.LIGHT_ONLY
            | EffectFeatures.HI_DEF
            | EffectFeatures.LIGHT_ONLY_HI_DEF
        )


@dataclass(frozen=True)
class Effect:
    flags: set[EffectFlag]
    features: EffectFeatures = EffectFeatures.NONE


@dataclass(frozen=True)
class EffectCode:
    hlsl: str
    declarations: str
    initializers: str
    selector: str


def generate_effect_code(
    effect_type_name: str,
    vertex_shader_name: str,
    base_function_name: str,
    pixel_shader_input_type_name: str,
    pixel_shader_input_member_names: list[str],
    all_flags: list[EffectFlag],
    effects: list[Effect],
) -> EffectCode:
    hlsl_shader_functions = ""
    hlsl_techniques = ""
    effect_loader_names: list[str] = []
    for effect in effects:
        effect_name = ""
        for flag in all_flags:
            effect_name += flag.name(flag in effect.flags)

        feature_flags: list[tuple[bool, bool]] = []
        if EffectFeatures.NONE in effect.features:
            feature_flags.append((False, False))
        if EffectFeatures.LIGHT_ONLY in effect.features:
            feature_flags.append((True, False))
        if EffectFeatures.HI_DEF in effect.features:
            feature_flags.append((False, True))
        if EffectFeatures.LIGHT_ONLY_HI_DEF in effect.features:
            feature_flags.append((True, True))

        for light_only, hi_def in feature_flags:
            function, technique = _generate_hlsl(
                vertex_shader_name,
                base_function_name,
                pixel_shader_input_type_name,
                pixel_shader_input_member_names,
                all_flags,
                effect.flags,
                effect_name,
                light_only,
                hi_def,
            )
            hlsl_shader_functions += f"{function}\n"
            hlsl_techniques += f"{technique}\n"

        if effect.features == EffectFeatures.LIGHT_ONLY:
            effect_name += "LightOnly"
        elif effect.features == EffectFeatures.HI_DEF:
            effect_name += "HiDef"
        elif effect.features == EffectFeatures.LIGHT_ONLY_HI_DEF:
            effect_name += "LightOnlyHiDef"

        effect_loader_names.append(effect_name)

    hlsl = f"{hlsl_shader_functions}\n{hlsl_techniques}"

    declarations = ""
    effect_field_names: list[str] = []
    for effect in effects:
        effect_name = ""
        for flag in all_flags:
            effect_name += flag.name(flag in effect.flags)
        effect_name = "_" + effect_name[0].lower() + effect_name[1:]

        if effect.features == EffectFeatures.LIGHT_ONLY:
            effect_name += "LightOnly"
        elif effect.features == EffectFeatures.HI_DEF:
            effect_name += "HiDef"
        elif effect.features == EffectFeatures.LIGHT_ONLY_HI_DEF:
            effect_name += "LightOnlyHiDef"

        effect_name += "Effect"
        effect_field_names.append(effect_name)

        declarations += f"private readonly {effect_type_name} {effect_name};\n"

    initializers = ""
    for effect_field_name, effect_loader_name, effect in zip(
        effect_field_names, effect_loader_names, effects
    ):
        features_name = ""
        if (
            effect.features == EffectFeatures.LIGHT_ONLY
            or effect.features == EffectFeatures.HI_DEF
            or effect.features == EffectFeatures.LIGHT_ONLY_HI_DEF
        ):
            pass
        elif EffectFeatures.NONE not in effect.features:
            raise ValueError(f"invalid effect features for effect {effect_loader_name}")
        elif (
            EffectFeatures.LIGHT_ONLY in effect.features
            and EffectFeatures.HI_DEF in effect.features
            and EffectFeatures.LIGHT_ONLY_HI_DEF in effect.features
        ):
            features_name += "EffectFeatures.All"
        else:
            if EffectFeatures.LIGHT_ONLY in effect.features:
                features_name += "EffectFeatures.LightOnly"
            if EffectFeatures.HI_DEF in effect.features:
                if features_name:
                    features_name += " | "
                features_name += "EffectFeatures.HiDef"
            if EffectFeatures.LIGHT_ONLY_HI_DEF in effect.features:
                if features_name:
                    features_name += " | "
                features_name += "EffectFeatures.LightOnlyHiDef"

        initializers += (
            f"{effect_field_name} = new(effect, {json.dumps(effect_loader_name)}"
        )
        if features_name:
            initializers += f", {features_name}"
        initializers += f");\n"

    selector = _generate_selector(all_flags, effects, effect_field_names)

    return EffectCode(hlsl, declarations, initializers, selector)


def _generate_hlsl(
    vertex_shader_name: str,
    base_function_name: str,
    pixel_shader_input_type_name: str,
    pixel_shader_input_member_names: list[str],
    all_flags: list[EffectFlag],
    effect_flags: set[EffectFlag],
    effect_name: str,
    light_only: bool,
    hi_def: bool,
) -> tuple[str, str]:
    technique_name = effect_name
    if light_only:
        technique_name += "LightOnly"
    if hi_def:
        technique_name += "HiDef"
    pixel_shader_name = technique_name + "_PS"

    function = (
        f"float4 {pixel_shader_name}({pixel_shader_input_type_name} input) : COLOR0\n"
    )
    function += "{\n"
    function += f"    return {base_function_name}(\n"

    function += "        "
    for input_member_name in pixel_shader_input_member_names:
        function += f"input.{input_member_name}, "

    function = function[:-1] + "\n"
    function += "        "
    for flag in all_flags:
        if flag in effect_flags:
            function += "true, "
        else:
            function += "false, "

    if light_only:
        function += "true, "
    else:
        function += "false, "
    if hi_def:
        function += "true\n"
    else:
        function += "false\n"

    function += "    );\n"
    function += "}\n"

    technique = f"""technique {technique_name}
{{
    pass Pass1
    {{
        VertexShader = compile vs_3_0 {vertex_shader_name}();
        PixelShader = compile ps_3_0 {pixel_shader_name}();
    }}
}}
"""

    return function, technique


def _generate_selector(
    flags: list[EffectFlag], effects: list[Effect], effect_names: list[str]
) -> str:
    if not flags:
        if len(effects) != 1:
            raise ValueError("failed to generate selector")

        return effect_names[0]

    enabled_effects: list[Effect] = []
    enabled_names: list[str] = []
    disabled_effects: list[Effect] = []
    disabled_names: list[str] = []

    flag = flags[0]
    for effect, effect_name in zip(effects, effect_names):
        if flag in effect.flags:
            enabled_effects.append(effect)
            enabled_names.append(effect_name)
        else:
            disabled_effects.append(effect)
            disabled_names.append(effect_name)

    flag_name = flag.name(True)
    flag_name = flag_name[0].lower() + flag_name[1:] + "EffectFlag"
    next_flags = flags[1:]
    if not disabled_effects:
        return _generate_selector(next_flags, enabled_effects, enabled_names)
    elif not enabled_effects:
        return _generate_selector(next_flags, disabled_effects, disabled_names)
    else:
        return f"{flag_name} ? {_generate_selector(next_flags, enabled_effects, enabled_names)} : {_generate_selector(next_flags, disabled_effects, disabled_names)}"
