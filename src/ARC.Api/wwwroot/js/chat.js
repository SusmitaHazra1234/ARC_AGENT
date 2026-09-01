const STORAGE_KEY = "arc.chat.session";
const APP_NAME = "Legal Outstanding Chat";

const ROLES = [
  { value: "Tsi", label: "TSI" },
  { value: "DepotManager", label: "Depot Manager" },
  { value: "Legal", label: "Legal" },
  { value: "Advocate", label: "Advocate" },
  { value: "DepotAdmin", label: "Depot Admin" },
  { value: "Finance", label: "Finance" },
];

const ONBOARDING = [
  {
    key: "dealerUrn",
    prompt: `Welcome to **${APP_NAME}** — your legal outstanding intelligence partner.\n\nLet's set up your session. What is the **Dealer URN**? *(e.g. dealer:s1)*`,
    placeholder: "e.g. dealer:s1",
    validate: (v) => v.length > 0 || "Please enter a dealer URN.",
  },
  {
    key: "cycleId",
    prompt: "Got it. Which **Cycle ID** are you working on? *(e.g. 2026-03-chat)*",
    placeholder: "e.g. 2026-03-chat",
    validate: (v) => v.length > 0 || "Please enter a cycle ID.",
  },
  {
    key: "upn",
    prompt: "What is your **UPN** (work email)?",
    placeholder: "you@company.com",
    validate: (v) => (v.includes("@") ? true : "Please enter a valid email / UPN."),
  },
  {
    key: "role",
    prompt: "What is your **role**? Choose one below or type it.",
    placeholder: "Select or type your role",
    type: "role",
    validate: (v) => ROLES.some((r) => r.value.toLowerCase() === v.toLowerCase()) || "Please pick a valid role.",
  },
  {
    key: "region",
    prompt: "Last one — which **region** do you cover? *(e.g. West)*",
    placeholder: "e.g. West",
    validate: (v) => v.length > 0 || "Please enter your region.",
  },
];

const chatThread = document.getElementById("chatThread");
const chatBody = document.getElementById("chatBody");
const chatForm = document.getElementById("chatForm");
const messageInput = document.getElementById("messageInput");
const sendBtn = document.getElementById("sendBtn");
const sessionSummary = document.getElementById("sessionSummary");
const resetSessionBtn = document.getElementById("resetSession");
const quickReplies = document.getElementById("quickReplies");
const headerStatus = document.getElementById("headerStatus");
const sidebar = document.getElementById("sidebar");
const sidebarToggle = document.getElementById("sidebarToggle");
const sidebarBackdrop = document.getElementById("sidebarBackdrop");

let session = loadSession();
let onboardingStep = session.complete ? ONBOARDING.length : 0;

function loadSession() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return emptySession();
    const parsed = JSON.parse(raw);
    return parsed.complete ? parsed : emptySession();
  } catch {
    return emptySession();
  }
}

function emptySession() {
  return {
    dealerUrn: "",
    cycleId: "",
    upn: "",
    role: "Tsi",
    region: "",
    complete: false,
  };
}

function saveSession() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  updateSessionSummary();
}

function updateSessionSummary() {
  if (!session.complete) {
    sessionSummary.innerHTML = '<p class="empty-state">Session setup in progress…</p>';
    return;
  }

  sessionSummary.innerHTML = `
    <div><dt>Dealer</dt><dd>${escapeHtml(session.dealerUrn)}</dd></div>
    <div><dt>Cycle</dt><dd>${escapeHtml(session.cycleId)}</dd></div>
    <div><dt>UPN</dt><dd>${escapeHtml(session.upn)}</dd></div>
    <div><dt>Role</dt><dd>${escapeHtml(formatRole(session.role))}</dd></div>
    <div><dt>Region</dt><dd>${escapeHtml(session.region)}</dd></div>
  `;
}

function closeSidebar() {
  sidebar.classList.remove("open");
  sidebarBackdrop.hidden = true;
}

function toggleSidebar() {
  const open = sidebar.classList.toggle("open");
  sidebarBackdrop.hidden = !open;
}

function formatRole(value) {
  return ROLES.find((r) => r.value === value)?.label ?? value;
}

function escapeHtml(text) {
  const el = document.createElement("span");
  el.textContent = text;
  return el.innerHTML;
}

function formatTime(date) {
  return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

function renderMarkdownLite(text) {
  return escapeHtml(text)
    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
    .replace(/\*([^*]+)\*/g, "<em>$1</em>")
    .replace(/\n/g, "<br>");
}

function appendMessage(text, type, agent = APP_NAME) {
  const wrap = document.createElement("div");
  wrap.className = `message ${type}`;

  const bubble = document.createElement("div");
  bubble.className = "bubble";

  const p = document.createElement("p");
  p.innerHTML = renderMarkdownLite(text);
  bubble.appendChild(p);

  const meta = document.createElement("span");
  meta.className = "meta";
  meta.textContent = type === "sent" ? formatTime(new Date()) : `${agent} · ${formatTime(new Date())}`;
  bubble.appendChild(meta);

  wrap.appendChild(bubble);
  chatThread.appendChild(wrap);
  chatBody.scrollTop = chatBody.scrollHeight;
  return wrap;
}

function showTyping() {
  const wrap = document.createElement("div");
  wrap.className = "message received";
  wrap.id = "typingIndicator";

  const bubble = document.createElement("div");
  bubble.className = "bubble typing";
  bubble.innerHTML = '<span class="typing-dot"></span><span class="typing-dot"></span><span class="typing-dot"></span>';
  wrap.appendChild(bubble);
  chatThread.appendChild(wrap);
  chatBody.scrollTop = chatBody.scrollHeight;
}

function hideTyping() {
  document.getElementById("typingIndicator")?.remove();
}

function hideQuickReplies() {
  quickReplies.hidden = true;
  quickReplies.innerHTML = "";
}

function showRoleChips(onSelect) {
  quickReplies.innerHTML = "";
  ROLES.forEach((role) => {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "chip";
    btn.textContent = role.label;
    btn.addEventListener("click", () => onSelect(role.value));
    quickReplies.appendChild(btn);
  });
  quickReplies.hidden = false;
}

function setInputPlaceholder(text) {
  messageInput.placeholder = text || "Type a message…";
}

function normalizeRole(input) {
  const match = ROLES.find((r) => r.value.toLowerCase() === input.toLowerCase()
    || r.label.toLowerCase() === input.toLowerCase());
  return match?.value ?? input;
}

function startOnboarding() {
  onboardingStep = 0;
  session = emptySession();
  chatThread.innerHTML = "";
  updateSessionSummary();
  hideQuickReplies();
  askOnboardingQuestion();
}

function askOnboardingQuestion() {
  const step = ONBOARDING[onboardingStep];
  if (!step) return;

  setTimeout(() => {
    appendMessage(step.prompt, "received");
    setInputPlaceholder(step.placeholder);

    if (step.type === "role") {
      showRoleChips((value) => handleOnboardingAnswer(value));
    } else {
      hideQuickReplies();
    }
  }, 400);
}

function finishOnboarding() {
  session.complete = true;
  saveSession();
  hideQuickReplies();
  setInputPlaceholder("Ask about exposure, notices, legal eligibility…");

  headerStatus.innerHTML = '<span class="status-dot"></span>Online · session active';

  setTimeout(() => {
    appendMessage(
      `You're all set, **${formatRole(session.role)}**.\n\nAsk me about dealer exposure, notice decisions, legal eligibility — or type **help** anytime.`,
      "received"
    );
  }, 500);
}

function handleOnboardingAnswer(raw) {
  const step = ONBOARDING[onboardingStep];
  if (!step) return;

  let value = raw.trim();
  if (step.type === "role") {
    value = normalizeRole(value);
  }

  const valid = step.validate(value);
  if (valid !== true) {
    appendMessage(value || "(empty)", "sent");
    setTimeout(() => appendMessage(valid, "received"), 300);
    return;
  }

  appendMessage(step.type === "role" ? formatRole(value) : value, "sent");
  session[step.key] = value;
  hideQuickReplies();
  onboardingStep += 1;

  if (onboardingStep >= ONBOARDING.length) {
    finishOnboarding();
    return;
  }

  showTyping();
  setTimeout(() => {
    hideTyping();
    askOnboardingQuestion();
  }, 600);
}

async function sendApiMessage(text) {
  showTyping();
  sendBtn.disabled = true;

  try {
    const response = await fetch("/v1/chat/messages", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Arc-Upn": session.upn,
        "X-Arc-Role": session.role,
        "X-Arc-Region": session.region,
      },
      body: JSON.stringify({
        message: text,
        dealerUrn: session.dealerUrn || null,
        cycleId: session.cycleId || null,
        region: session.region || null,
      }),
    });

    hideTyping();

    if (!response.ok) {
      const err = await response.json().catch(() => ({}));
      appendMessage(err.error || `Request failed (${response.status})`, "received");
      return;
    }

    const data = await response.json();
    appendMessage(data.reply, "received", data.agent || APP_NAME);
  } catch (error) {
    hideTyping();
    appendMessage(`Network error: ${error.message}`, "received");
  } finally {
    sendBtn.disabled = false;
    messageInput.focus();
  }
}

function handleSubmit(text) {
  if (!text) return;

  if (!session.complete) {
    handleOnboardingAnswer(text);
    return;
  }

  appendMessage(text, "sent");
  sendApiMessage(text);
}

sidebarToggle.addEventListener("click", toggleSidebar);
sidebarBackdrop.addEventListener("click", closeSidebar);

resetSessionBtn.addEventListener("click", () => {
  localStorage.removeItem(STORAGE_KEY);
  closeSidebar();
  headerStatus.innerHTML = '<span class="status-dot"></span>Setting up your session…';
  startOnboarding();
});

chatForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const text = messageInput.value.trim();
  if (!text) return;
  messageInput.value = "";
  handleSubmit(text);
});

updateSessionSummary();

if (session.complete) {
  appendMessage(
    `Welcome back! Session active for **${session.dealerUrn}** · cycle **${session.cycleId}**.\n\nHow can I help you today?`,
    "received"
  );
  setInputPlaceholder("Ask about exposure, notices, legal eligibility…");
} else {
  headerStatus.innerHTML = '<span class="status-dot"></span>Setting up your session…';
  askOnboardingQuestion();
}

messageInput.focus();
