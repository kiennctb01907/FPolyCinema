/* Quên mật khẩu 2 bước (UI C6, SPEC 2.5): gửi mã OTP, nhập 6 số, đếm ngược 5 phút, gửi lại sau 60 giây. */
(() => {
  'use strict';
  const root = document.getElementById('forgot');
  if (!root) return;
  const step = (n) => root.querySelectorAll('[data-step]').forEach((s) => { s.hidden = s.dataset.step !== String(n); });
  const emailForm = document.getElementById('forgot-email');
  const otpForm = document.getElementById('forgot-otp');
  const emailInput = document.getElementById('fp-email');
  const otpHidden = document.getElementById('fp-otp');
  const otpBox = root.querySelector('[data-otp]');
  const otpError = root.querySelector('[data-otp-error]');
  const countdown = root.querySelector('[data-otp-countdown]');
  const resend = root.querySelector('[data-resend]');
  const resendWrap = root.querySelector('[data-resend-wrap]');
  const resendCount = root.querySelector('[data-resend-count]');
  const len = Number(root.dataset.otpLength) || 6;
  let email = '', timers = [];
  const pad = (n) => String(n).padStart(2, '0');
  const clearTimers = () => { timers.forEach(clearInterval); timers = []; };

  function setOtpError(msg) {
    otpError.hidden = !msg;
    otpError.textContent = msg || '';
    otpBox.querySelectorAll('input').forEach((i) => i.classList.toggle('is-invalid', !!msg));
  }

  function startTimers(validSeconds, resendAfter) {
    clearTimers();
    const validEnd = Date.now() + validSeconds * 1000;
    const resendEnd = Date.now() + resendAfter * 1000;
    const tick = () => {
      const left = Math.max(0, Math.round((validEnd - Date.now()) / 1000));
      countdown.textContent = `${pad(Math.floor(left / 60))}:${pad(left % 60)}`;
      const r = Math.max(0, Math.round((resendEnd - Date.now()) / 1000));
      resend.disabled = r > 0;
      resendCount.textContent = r > 0 ? `(${r}s)` : '';
      if (resendWrap) {
        const tip = r > 0 ? `Bạn chờ ${r} giây nữa rồi mới gửi lại được mã.` : 'Gửi một mã mới tới email của bạn.';
        resendWrap.setAttribute('data-bs-original-title', tip);
        window.bootstrap?.Tooltip.getInstance(resendWrap)?.setContent({ '.tooltip-inner': tip });
      }
      if (left === 0) setOtpError('Mã xác nhận đã hết hạn. Bấm Gửi lại mã để nhận mã mới.');
    };
    tick();
    timers.push(setInterval(tick, 1000));
  }

  async function requestOtp(button) {
    const data = await ui.api('/Login/ForgotPassword', { method: 'POST', body: { email }, button, form: emailForm });
    root.querySelector('[data-sent-message]').textContent = data.message;
    root.querySelector('[data-email]').textContent = email;
    step(2);
    setOtpError('');
    ui.otpReset?.(otpBox);
    startTimers(data.validSeconds, data.resendAfter);
  }

  emailForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    email = emailInput.value.trim();
    if (!email || !emailInput.checkValidity()) {
      ui.showFieldErrors(emailForm, { email: 'Bạn nhập email đúng định dạng, ví dụ ten@gmail.com.' });
      ui.shake(emailInput);
      return;
    }
    try { await requestOtp(emailForm.querySelector('button[type="submit"]')); } catch { /* đã báo bằng toast */ }
  });

  resend.addEventListener('click', async () => {
    try { await requestOtp(resend); ui.toast.info('Đã gửi mã mới. Bạn kiểm tra hộp thư.'); } catch { /* đã báo */ }
  });

  root.querySelector('[data-back]').addEventListener('click', () => { clearTimers(); step(1); emailInput.focus(); });

  otpForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    const otp = otpHidden.value;
    if (otp.length !== len) { setOtpError(`Bạn nhập đủ ${len} chữ số của mã xác nhận.`); ui.shake(otpBox); return; }
    try {
      const data = await ui.api('/Login/VerifyOtp', { method: 'POST', body: { email, otp }, button: otpForm.querySelector('button[type="submit"]'), silent: true });
      clearTimers();
      step(3);
      setTimeout(() => { location.href = data.redirect || '/Login/Login'; }, 2500);
    } catch (err) {
      if (err.code === 'OTP_INVALID' || err.code === 'OTP_EXPIRED' || err.code === 'VALIDATION_FAILED') {
        setOtpError(err.message);
        ui.shake(otpBox);
        if (err.code === 'OTP_EXPIRED' || (err.details && err.details.remaining === 0)) resend.disabled = false;
        ui.otpReset?.(otpBox);
      } else {
        ui.toast.error(err.message);
      }
    }
  });
})();
