"""Security regression tests; all test keys are generated at runtime, never real repository secrets."""
import contextlib,importlib.util,io,os,secrets,subprocess,sys,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('manage',Path(__file__).with_name('manage.py'))
manage=importlib.util.module_from_spec(spec);spec.loader.exec_module(manage)

class SecretStoreTests(unittest.TestCase):
 def test_missing_required_secret_blocks_before_infrastructure(self):
  with tempfile.TemporaryDirectory() as directory:
   target=Path(directory)/'must-not-exist'
   with patch.dict(os.environ,{'CI_SECRET_SEED':'','REQUIRE_SECRET_STORE':'true'}),patch.object(manage,'STATE',target),patch.object(manage,'command',side_effect=AssertionError('Infrastructure must not start')):
    with self.assertRaisesRegex(RuntimeError,'infraestrutura bloqueada'):manage.init()
    self.assertFalse(target.exists())

 def test_vault_key_derives_distinct_passwords_and_is_not_inherited(self):
  seed=secrets.token_hex(32)
  passwords=[]
  for _ in range(2):
   with patch.dict(os.environ,{'CI_SECRET_SEED':seed,'REQUIRE_SECRET_STORE':'true','GITHUB_ACTIONS':'false'}):
    password,source=manage.database_password();passwords.append(password)
    self.assertEqual(source,'github_actions_secrets');self.assertNotIn(seed,password)
    self.assertNotIn('CI_SECRET_SEED',os.environ)
    result=subprocess.check_output([sys.executable,'-c',"import os; print('present' if 'CI_SECRET_SEED' in os.environ else 'absent')"],text=True).strip()
    self.assertEqual(result,'absent')
  self.assertNotEqual(passwords[0],passwords[1])

 def test_invalid_key_is_rejected_without_disclosure(self):
  bad=secrets.token_urlsafe(16)
  with patch.dict(os.environ,{'CI_SECRET_SEED':bad,'REQUIRE_SECRET_STORE':'true'}):
   with self.assertRaises(RuntimeError) as raised:manage.database_password()
   self.assertNotIn(bad,str(raised.exception))

if __name__=='__main__':unittest.main(verbosity=2)
