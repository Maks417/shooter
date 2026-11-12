"""Synthetic containment/rejection tests, never hardware evidence."""
import unittest
from render_budget import METHOD, render_budget_samples

class RenderBudgetTests(unittest.TestCase):
    def setUp(self):
        self.meta=dict(renderBudgetMethod=METHOD,timestampFrequencyMatches=True,nativeCpuFrequency=1000000,stopwatchFrequency=1000000)
        self.rows=[dict(nativeFrameStartTimestamp=10000,nativePresentCalledTimestamp=11000,nativeFirstSubmitTimestamp=10500,
                       lowerTimestamp=9900,upperTimestamp=10100,endTimestamp=12000,ambiguous='False',renderMs=.3),
                   dict(nativeFrameStartTimestamp=12000,nativePresentCalledTimestamp=13000,nativeFirstSubmitTimestamp=12500,
                       lowerTimestamp=11900,upperTimestamp=12100,endTimestamp=14000,ambiguous='False',renderMs=-1)]
    def test_unknown_native_work_is_not_replaced_but_complete_larger_bound_can_certify_budget(self):
        values,proof=render_budget_samples(self.meta,self.rows)
        self.assertEqual(values,[1.002,1.002]);self.assertEqual(self.rows[1]['renderMs'],-1)
        self.assertEqual(proof['nativeWorkSamples'],1);self.assertEqual(proof['nativeWorkUnavailable'],1)
    def test_missing_endpoint_and_clock_are_pending(self):
        self.rows[1]['nativePresentCalledTimestamp']=0
        self.assertIsNone(render_budget_samples(self.meta,self.rows)[0])
        self.rows[1]['nativePresentCalledTimestamp']=13000;self.meta['timestampFrequencyMatches']=False
        self.assertIsNone(render_budget_samples(self.meta,self.rows)[0])
    def test_known_work_outside_envelope_rejects_the_proposed_method(self):
        self.rows[0]['renderMs']=2
        with self.assertRaisesRegex(ValueError,'exceeds'):render_budget_samples(self.meta,self.rows)
    def test_reordered_clock_domains_and_submission_are_rejected(self):
        for key,value in [('nativePresentCalledTimestamp',9999),('nativeFirstSubmitTimestamp',14000),('nativeFrameStartTimestamp',15000)]:
            old=self.rows[0][key];self.rows[0][key]=value
            with self.assertRaises(ValueError):render_budget_samples(self.meta,self.rows)
            self.rows[0][key]=old
    def test_entirely_unknown_native_work_cannot_validate_containment(self):
        self.rows[0]['renderMs']=-1
        self.assertIsNone(render_budget_samples(self.meta,self.rows)[0])
    def test_broader_measured_window_can_fail_even_when_active_native_work_is_fast(self):
        self.rows[0]['nativePresentCalledTimestamp']=20000;self.rows[0]['endTimestamp']=21000
        self.rows[1]['nativeFrameStartTimestamp']=22000;self.rows[1]['lowerTimestamp']=21900;self.rows[1]['upperTimestamp']=22100;self.rows[1]['endTimestamp']=24000;self.rows[1]['nativeFirstSubmitTimestamp']=22500;self.rows[1]['nativePresentCalledTimestamp']=23000
        values,_=render_budget_samples(self.meta,self.rows)
        self.assertGreater(max(values),8) # The unchanged budget becomes harder, not easier.

if __name__=='__main__':unittest.main()
