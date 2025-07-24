// サイト全体で使用するJavaScript関数

// エラーハンドリングの改善
window.addEventListener('unhandledrejection', function(event) {
    console.error('Unhandled promise rejection:', event.reason);
});

// 共通のユーティリティ関数
const Utils = {
    // APIエラーメッセージを整形
    formatErrorMessage: function(error) {
        if (error.response && error.response.data) {
            return error.response.data.message || error.response.data;
        }
        return error.message || 'An unexpected error occurred';
    },

    // 時間をフォーマット
    formatTime: function(date) {
        return new Intl.DateTimeFormat('ja-JP', {
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit'
        }).format(date);
    },

    // 成功メッセージを表示
    showSuccess: function(message, elementId) {
        const element = document.getElementById(elementId);
        if (element) {
            element.innerHTML = `<div class="alert alert-success">✅ ${message}</div>`;
        }
    },

    // エラーメッセージを表示
    showError: function(message, elementId) {
        const element = document.getElementById(elementId);
        if (element) {
            element.innerHTML = `<div class="alert alert-danger">❌ ${message}</div>`;
        }
    },

    // 読み込み中の表示
    showLoading: function(elementId) {
        const element = document.getElementById(elementId);
        if (element) {
            element.innerHTML = '<div class="d-flex align-items-center"><div class="spinner-border spinner-border-sm me-2" role="status"></div>実行中...</div>';
        }
    }
};

// ページ読み込み完了時の処理
document.addEventListener('DOMContentLoaded', function() {
    // Bootstrap tooltipの初期化（必要に応じて）
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // フォームの入力値検証
    const numberInputs = document.querySelectorAll('input[type="number"]');
    numberInputs.forEach(input => {
        input.addEventListener('input', function() {
            const value = parseInt(this.value);
            const min = parseInt(this.getAttribute('min'));
            const max = parseInt(this.getAttribute('max'));
            
            if (value < min || value > max) {
                this.classList.add('is-invalid');
            } else {
                this.classList.remove('is-invalid');
            }
        });
    });
});

// デバッグ用のログ関数
function debugLog(message, data) {
    if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
        console.log(`[DEBUG] ${message}`, data);
    }
}
