namespace Server;

/// <summary>
/// Self-contained login / first-run setup page, served inline via a minimal API endpoint
/// (there's no Server/wwwroot, so a static file isn't worth the ceremony). Styled to match
/// the app's dark palette from Client/wwwroot/css/app.css.
/// </summary>
public static class LoginPage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>LineFlow &mdash; Sign in</title>
<style>
  * { box-sizing: border-box; }
  body {
    margin: 0;
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    background: #1a1a2e;
    color: #fff;
    font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
  }
  .card {
    width: 100%;
    max-width: 360px;
    background: #16213e;
    border: 1px solid #0f3460;
    border-radius: 10px;
    padding: 32px;
  }
  h1 {
    margin: 0 0 4px;
    font-size: 22px;
    color: #e94560;
  }
  p.sub {
    margin: 0 0 24px;
    color: #888;
    font-size: 13px;
  }
  label {
    display: block;
    font-size: 13px;
    color: #ccc;
    margin: 14px 0 6px;
  }
  input {
    width: 100%;
    padding: 9px 10px;
    background: #0f3460;
    border: 1px solid #1a4a8a;
    border-radius: 6px;
    color: #fff;
    font-size: 14px;
  }
  input:focus {
    outline: none;
    border-color: #e94560;
  }
  button {
    width: 100%;
    margin-top: 22px;
    padding: 10px;
    background: #e94560;
    border: 1px solid #e94560;
    border-radius: 6px;
    color: #fff;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
  }
  button:hover { background: #d13851; }
  button:disabled { opacity: 0.6; cursor: default; }
  .error {
    margin-top: 14px;
    padding: 8px 10px;
    background: rgba(233, 69, 96, 0.15);
    border: 1px solid #e94560;
    border-radius: 6px;
    color: #ff8fa3;
    font-size: 13px;
    display: none;
  }
</style>
</head>
<body>
  <div class="card">
    <h1 id="title">Sign in</h1>
    <p class="sub" id="subtitle">Enter your LineFlow credentials.</p>
    <form id="form">
      <label for="username">Username</label>
      <input id="username" name="username" autocomplete="username" required autofocus />

      <label for="password">Password</label>
      <input id="password" name="password" type="password" autocomplete="current-password" required />

      <div id="confirmGroup" style="display:none;">
        <label for="confirm">Confirm password</label>
        <input id="confirm" name="confirm" type="password" autocomplete="new-password" />
      </div>

      <button id="submitBtn" type="submit">Sign in</button>
      <div class="error" id="error"></div>
    </form>
  </div>

<script>
  const form = document.getElementById('form');
  const title = document.getElementById('title');
  const subtitle = document.getElementById('subtitle');
  const confirmGroup = document.getElementById('confirmGroup');
  const confirmInput = document.getElementById('confirm');
  const submitBtn = document.getElementById('submitBtn');
  const errorBox = document.getElementById('error');
  let setupMode = false;

  function showError(msg) {
    errorBox.textContent = msg;
    errorBox.style.display = 'block';
  }

  async function init() {
    try {
      const res = await fetch('/api/auth/status');
      const status = await res.json();
      if (status.isAuthenticated) {
        window.location.href = '/';
        return;
      }
      if (!status.hasUsers) {
        setupMode = true;
        title.textContent = 'Create admin account';
        subtitle.textContent = 'No accounts exist yet. Create the first account (it will be an administrator).';
        confirmGroup.style.display = 'block';
        confirmInput.required = true;
        submitBtn.textContent = 'Create account';
      }
    } catch (e) {
      showError('Could not reach the server. Please try again.');
    }
  }

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    errorBox.style.display = 'none';

    const username = document.getElementById('username').value.trim();
    const password = document.getElementById('password').value;

    if (setupMode && password !== confirmInput.value) {
      showError('Passwords do not match.');
      return;
    }

    submitBtn.disabled = true;
    try {
      const res = await fetch(setupMode ? '/api/auth/setup' : '/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password })
      });
      if (res.ok) {
        window.location.href = '/';
        return;
      }
      const body = await res.json().catch(() => ({}));
      showError(body.error || 'Sign in failed.');
    } catch (e) {
      showError('Could not reach the server. Please try again.');
    } finally {
      submitBtn.disabled = false;
    }
  });

  init();
</script>
</body>
</html>
""";
}
