import unittest
from shader_build_counts import parse


def log():
    lines = ['Compiling shader "Shooter/ProceduralSurface"']
    for name, stage, full, settings, retained in [("ForwardLit", "vp", 12, 8, 2), ("ForwardLit", "fp", 24, 16, 4), ("ShadowCaster", "vp", 1, 1, 1), ("ShadowCaster", "fp", 1, 1, 1)]:
        lines += [f'  Pass "{name}" ({stage})', '    Target graphics API: d3d11', f'    Full variant space:         {full}',
                  f'    After settings filtering:   {settings}', f'    After built-in stripping:   {retained}', f'    After scriptable stripping: {retained}']
    return ("\n".join(lines) + '\nSerialized binary data for shader Shooter/ProceduralSurface in 0.00s\n    d3d11 (total internal programs: 8, unique: 8)\nCompiling shader "Other"\n').encode()


class ShaderBuildCountTests(unittest.TestCase):
    def test_actual_stage_counts_are_not_pipeline_count(self):
        proof = parse(log())
        self.assertEqual(proof["retainedStagePrograms"], 8)
        self.assertEqual([stage["scriptable"] for stage in proof["stages"]], [2, 4, 1, 1])
        self.assertIsNone(proof["pipelineVariantCount"])
        self.assertEqual(proof["sectionLine"], 1)

    def test_missing_duplicate_or_changed_sections_reject(self):
        mutations = [log().replace(b'Shooter/ProceduralSurface', b'OtherShader'), log() + log(),
                     log().replace(b'  Pass "ShadowCaster" (fp)', b'  Pass "Unknown" (fp)'),
                     log().replace(b'    After scriptable stripping: 4', b'    After scriptable stripping: 5'),
                     log().replace(b'Target graphics API: d3d11', b'Target graphics API: vulkan'),
                     log().replace(b'unique: 8', b'unique: 9'), log().replace(b'total internal programs: 8', b'total internal programs: 7'),
                     log().replace(b'    After scriptable stripping: 4\n', b'')]
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                with self.assertRaises(ValueError): parse(mutation)

    def test_empty_input_rejects(self):
        with self.assertRaises(ValueError): parse(b'')

    def test_excessive_line_count_rejects_before_materializing_lines(self):
        with self.assertRaisesRegex(ValueError, "500000"):
            parse(b'\n' * 500001)


if __name__ == "__main__":
    unittest.main()
