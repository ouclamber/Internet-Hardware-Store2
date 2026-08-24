import React, { Component } from 'react';
import './AdminPanel.css';
import { withNavigate } from './withNavigate';
import CartIndicator from '../CartIndicator/CartIndicator';
import { CartContext } from '../CartContext/CartContext';

class AdminPanel extends Component {
    static contextType = CartContext;
    constructor(props) {
        super(props);
        this.state = {
            activeTab: 'dashboard',
            users: [],
            orders: [],
            stats: null,
            loading: true,
            error: null,
            totalOrdersCount: 0
        };
        this.abortController = null;
    }

    componentDidMount() {
        console.log('[AdminPanel] componentDidMount - проверка роли');
        const userRole = localStorage.getItem('userRole');
        console.log('[AdminPanel] userRole из localStorage:', userRole);
        
        if (userRole !== 'Admin') {
            alert('Доступ запрещен. Только для администраторов.');
            this.props.navigate('/HomePage');
            return;
        }
        console.log('[AdminPanel] Пользователь админ, загружаем дашборд');
        this.loadDashboard();
    }

    componentWillUnmount() {
        if (this.abortController) {
            this.abortController.abort();
        }
    }

    loadDashboard = async () => {
        console.log('[loadDashboard] НАЧАЛО загрузки дашборда');
        this.setState({ loading: true });
        try {
            const token = localStorage.getItem('token');
            console.log('[loadDashboard] Токен:', token ? 'Есть' : 'Нет');
            
            const response = await fetch('http://localhost:5214/api/Admin/stats', {
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                }
            });
            
            console.log('[loadDashboard] Статус ответа:', response.status);
            
            if (response.ok) {
                const stats = await response.json();
                console.log('[loadDashboard] Данные статистики:', stats);
                this.setState({ stats, loading: false });
            } else {
                const error = await response.text();
                console.error('[loadDashboard] Ошибка ответа:', error);
                this.setState({ error: 'Ошибка загрузки статистики', loading: false });
            }
        } catch (error) {
            console.error('[loadDashboard] Исключение:', error);
            this.setState({ error: 'Ошибка загрузки данных', loading: false });
        }
    };

    loadUsers = async () => {
        console.log('[loadUsers] НАЧАЛО загрузки пользователей');
        this.setState({ loading: true });
        try {
            const token = localStorage.getItem('token');
            console.log('[loadUsers] Токен:', token ? 'Есть' : 'Нет');
            
            const response = await fetch('http://localhost:5214/api/Admin/users', {
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                }
            });
            
            console.log('[loadUsers] Статус ответа:', response.status);
            
            if (response.ok) {
                const users = await response.json();
                console.log('[loadUsers] Получено пользователей:', users.length);
                this.setState({ users, loading: false });
            } else {
                const error = await response.text();
                console.error('[loadUsers] Ошибка ответа:', error);
                this.setState({ error: 'Ошибка загрузки пользователей', loading: false });
            }
        } catch (error) {
            console.error('[loadUsers] Исключение:', error);
            this.setState({ error: 'Ошибка загрузки данных', loading: false });
        }
    };

    loadOrders = async () => {
        console.log('[loadOrders] НАЧАЛО загрузки заказов');
        this.setState({ loading: true });
        try {
            const token = localStorage.getItem('token');
            console.log('[loadOrders] Токен:', token ? 'Есть' : 'Нет');
            
            const response = await fetch('http://localhost:5214/api/Admin/orders', {
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                }
            });
            
            console.log('[loadOrders] Статус ответа:', response.status);
            
            if (response.ok) {
                const data = await response.json();
                console.log('[loadOrders] ПОЛНЫЙ ОТВЕТ:', data);
                
                let ordersList = data.Orders || [];
                let totalCount = data.TotalCount || 0;
                
                this.setState({ 
                    orders: ordersList,
                    totalOrdersCount: totalCount,
                    loading: false 
                });
            } else {
                const error = await response.text();
                console.error('[loadOrders] Ошибка ответа:', error);
                this.setState({ error: 'Ошибка загрузки заказов', loading: false });
            }
        } catch (error) {
            console.error('[loadOrders] Исключение:', error);
            this.setState({ error: 'Ошибка загрузки данных', loading: false });
        }
    };

    updateUserRole = async (userId, newRole) => {
        console.log('[updateUserRole] НАЧАЛО, userId:', userId, 'newRole:', newRole);
        
        if (!window.confirm(`Изменить роль пользователя на "${newRole}"?`)) {
            console.log('[updateUserRole] Отменено пользователем');
            return;
        }

        try {
            const token = localStorage.getItem('token');
            console.log('[updateUserRole] Токен:', token ? 'Есть' : 'Нет');
            
            const response = await fetch(`http://localhost:5214/api/Admin/users/${userId}/role`, {
                method: 'PUT',
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ role: newRole })
            });
            
            console.log('[updateUserRole] Статус ответа:', response.status);
            
            if (response.ok) {
                const result = await response.json();
                console.log('[updateUserRole] Роль обновлена:', result);
                alert('Роль обновлена');
                await this.loadUsers();
            } else {
                const error = await response.json();
                console.error('[updateUserRole] Ошибка:', error);
                alert(error.message || 'Ошибка обновления роли');
            }
        } catch (error) {
            console.error('[updateUserRole] Исключение:', error);
            alert('Ошибка обновления роли');
        }
    };

    updateOrderStatus = async (orderId, newStatus) => {
        console.log('[updateOrderStatus] НАЧАЛО, orderId:', orderId, 'newStatus:', newStatus);
        
        try {
            const token = localStorage.getItem('token');
            console.log('[updateOrderStatus] Токен:', token ? 'Есть' : 'Нет');
            
            const response = await fetch(`http://localhost:5214/api/Admin/orders/${orderId}/status`, {
                method: 'PUT',
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ status: newStatus })
            });
            
            console.log('[updateOrderStatus] Статус ответа:', response.status);
            
            if (response.ok) {
                const result = await response.json();
                console.log('[updateOrderStatus] Статус обновлен:', result);
                alert('Статус обновлен');
                await this.loadOrders();
            } else {
                const error = await response.json();
                console.error('[updateOrderStatus] Ошибка:', error);
                alert(error.message || 'Ошибка обновления статуса');
            }
        } catch (error) {
            console.error('[updateOrderStatus] Исключение:', error);
            alert('Ошибка обновления статуса');
        }
    };

    deleteUser = async (userId) => {
        console.log('[deleteUser] НАЧАЛО, userId:', userId);
        
        if (!window.confirm('Удалить пользователя?')) {
            console.log('[deleteUser] Отменено пользователем');
            return;
        }

        try {
            const token = localStorage.getItem('token');
            console.log('[deleteUser] Токен:', token ? 'Есть' : 'Нет');
            
            const response = await fetch(`http://localhost:5214/api/Admin/users/${userId}`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });
            
            console.log('[deleteUser] Статус ответа:', response.status);
            
            if (response.ok) {
                const result = await response.json();
                console.log('[deleteUser] Пользователь удален:', result);
                alert('Пользователь удален');
                await this.loadUsers();
            } else {
                let errorMessage = 'Ошибка удаления';
                try {
                    const error = await response.json();
                    errorMessage = error.message || errorMessage;
                } catch (e) {
                    const text = await response.text();
                    console.error('[deleteUser] Текст ошибки:', text);
                    errorMessage = text || errorMessage;
                }
                console.error('[deleteUser] Ошибка:', errorMessage);
                alert(errorMessage);
            }
        } catch (error) {
            console.error('[deleteUser] Исключение:', error);
            alert('Ошибка удаления пользователя');
        }
    };

    switchTab = (tab) => {
        console.log('[switchTab] Переключение на вкладку:', tab);
        this.setState({ activeTab: tab, error: null });
        if (tab === 'users') {
            this.loadUsers();
        } else if (tab === 'orders') {
            this.loadOrders();
        } else {
            this.loadDashboard();
        }
    };

    handleGoBack = () => {
        this.props.navigate(-1);
    };

    handleProfile = () => {
        this.props.navigate('/Profile');
    };

    handleSearch = (e) => {
        e.preventDefault();
        const input = e.target.querySelector('input[type="search"]');
        const query = input?.value;
        if (query && query.trim()) {
            this.props.navigate(`/search?q=${encodeURIComponent(query.trim())}`);
        }
    };

    renderDashboard = () => {
        const { stats } = this.state;
        if (!stats) return <div className="empty-state">Нет данных</div>;

        return (
            <div className="admin-dashboard">
                <div className="stats-grid">
                    <div className="stat-card">
                        <div className="stat-info">
                            <h3>{stats.TotalUsers || stats.totalUsers || 0}</h3>
                            <p>Пользователей</p>
                        </div>
                    </div>
                    <div className="stat-card">
                        <div className="stat-info">
                            <h3>{stats.TotalProducts || stats.totalProducts || 0}</h3>
                            <p>Товаров</p>
                        </div>
                    </div>
                    <div className="stat-card">
                        <div className="stat-info">
                            <h3>{stats.TotalOrders || stats.totalOrders || 0}</h3>
                            <p>Заказов</p>
                        </div>
                    </div>
                    <div className="stat-card">
                        <div className="stat-info">
                            <h3>{stats.TotalRevenue || stats.totalRevenue ? (stats.TotalRevenue || stats.totalRevenue).toLocaleString('ru-RU') : '0'} ₽</h3>
                            <p>Выручка</p>
                        </div>
                    </div>
                </div>

                <div className="recent-orders">
                    <h3>Последние заказы</h3>
                    {stats.RecentOrders && stats.RecentOrders.length > 0 ? (
                        <table className="admin-table">
                            <thead>
                                <tr>
                                    <th>№ заказа</th>
                                    <th>Пользователь</th>
                                    <th>Сумма</th>
                                    <th>Статус</th>
                                    <th>Дата</th>
                                </tr>
                            </thead>
                            <tbody>
                                {stats.RecentOrders.map((order, index) => (
                                    <tr key={order.Id || order.id || index}>
                                        <td>{order.OrderNumber || order.orderNumber}</td>
                                        <td>{order.UserName || order.userName}</td>
                                        <td>{order.TotalAmount || order.totalAmount ? (order.TotalAmount || order.totalAmount).toLocaleString('ru-RU') : '0'} ₽</td>
                                        <td><span className={`status-badge status-${order.Status || order.status}`}>{order.Status || order.status}</span></td>
                                        <td>{order.CreatedAt || order.createdAt ? new Date(order.CreatedAt || order.createdAt).toLocaleDateString('ru-RU') : 'Нет данных'}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    ) : (
                        <p>Нет заказов</p>
                    )}
                </div>
            </div>
        );
    };

    renderUsers = () => {
        const { users } = this.state;
        
        if (!users || users.length === 0) {
            return <div className="empty-state">Нет пользователей</div>;
        }
        
        return (
            <div className="admin-users">
                <h3>Управление пользователями</h3>
                <table className="admin-table">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Имя</th>
                            <th>Роль</th>
                            <th>Дата регистрации</th>
                            <th>Корзина</th>
                            <th>Заказы</th>
                            <th>Действия</th>
                        </tr>
                    </thead>
                    <tbody>
                        {users.map((user, index) => {
                            const userId = user.Id || user.id;
                            const currentRole = user.Role || user.role || 'User';
                            
                            return (
                                <tr key={userId || index}>
                                    <td>{userId}</td>
                                    <td>{user.UserName || user.userName}</td>
                                    <td>
                                        <select 
                                            value={currentRole}
                                            onChange={(e) => {
                                                const newRole = e.target.value;
                                                this.updateUserRole(userId, newRole);
                                            }}
                                            className="role-select"
                                        >
                                            <option value="User">User</option>
                                            <option value="Admin">Admin</option>
                                        </select>
                                    </td>
                                    <td>
                                        {user.CreatedAt || user.createdAt 
                                            ? new Date(user.CreatedAt || user.createdAt).toLocaleDateString('ru-RU') 
                                            : 'Нет данных'}
                                    </td>
                                    <td>{user.BasketCount || user.basketCount || 0}</td>
                                    <td>{user.PurchaseCount || user.purchaseCount || 0}</td>
                                    <td>
                                        {currentRole !== 'Admin' && (
                                            <button 
                                                className="delete-btn"
                                                onClick={() => this.deleteUser(userId)}
                                            >
                                                Удалить
                                            </button>
                                        )}
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>
        );
    };

    renderOrders = () => {
        const { orders, totalOrdersCount } = this.state;
        
        return (
            <div className="admin-orders">
                <div className="orders-header">
                    <h3>Управление заказами</h3>
                    <span className="orders-total">Всего заказов: {totalOrdersCount || orders.length || 0}</span>
                </div>
                
                {orders && orders.length > 0 ? (
                    <table className="admin-table">
                        <thead>
                            <tr>
                                <th>№ заказа</th>
                                <th>Пользователь</th>
                                <th>Сумма</th>
                                <th>Статус</th>
                                <th>Дата</th>
                                <th>Действия</th>
                            </tr>
                        </thead>
                        <tbody>
                            {orders.map((order, index) => {
                                const orderId = order.Id || order.id;
                                const currentStatus = order.Status || order.status || 'pending';
                                
                                return (
                                    <tr key={orderId || index}>
                                        <td>{order.OrderNumber || order.orderNumber || 'Нет номера'}</td>
                                        <td>{order.UserName || order.userName || 'Неизвестно'}</td>
                                        <td>{order.TotalAmount || order.totalAmount ? (order.TotalAmount || order.totalAmount).toLocaleString('ru-RU') : '0'} ₽</td>
                                        <td>
                                            <select 
                                                value={currentStatus}
                                                onChange={(e) => {
                                                    const newStatus = e.target.value;
                                                    this.updateOrderStatus(orderId, newStatus);
                                                }}
                                                className="status-select"
                                            >
                                                <option value="pending">В обработке</option>
                                                <option value="paid">Оплачен</option>
                                                <option value="shipped">Отправлен</option>
                                                <option value="delivered">Доставлен</option>
                                                <option value="cancelled">Отменен</option>
                                            </select>
                                        </td>
                                        <td>
                                            {order.CreatedAt || order.createdAt 
                                                ? new Date(order.CreatedAt || order.createdAt).toLocaleDateString('ru-RU') 
                                                : 'Нет данных'}
                                        </td>
                                        <td>
                                            <button 
                                                className="delete-btn"
                                                onClick={() => {
                                                    if (window.confirm('Отменить заказ?')) {
                                                        this.updateOrderStatus(orderId, 'cancelled');
                                                    }
                                                }}
                                            >
                                                Отменить
                                            </button>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                ) : (
                    <p>Нет заказов</p>
                )}
            </div>
        );
    };

    render() {
        const { activeTab, loading, error } = this.state;

        return (
            <div className="admin-panel">
                <header className="header">
                    <nav className="nav container">
                        <a href="/" className="nav__logo">
                            <div className="logo-text">
                                <span className="logo-primary">GLANCE</span>
                                <span className="logo-secondary">vex</span>
                            </div>
                        </a>

                        <div className="search-full-width">
                            <div className="header-search-block">
                                <form onSubmit={this.handleSearch}>
                                    <input
                                        type="search"
                                        placeholder="Поиск товаров..."
                                        className="header-search-block__search"
                                    />
                                </form>
                            </div>
                        </div>

                        <div className="header-icons">
                            <button className="icon-btn user-icon" onClick={this.handleProfile}>
                                <svg width="20" height="20" viewBox="0 0 24 24" fill="black">
                                    <path d="M12 12C14.7614 12 17 9.76142 17 7C17 4.23858 14.7614 2 12 2C9.23858 2 7 4.23858 7 7C7 9.76142 9.23858 12 12 12Z" stroke="currentColor" strokeWidth="2"/>
                                    <path d="M20 22C20 17.5817 16.4183 14 12 14C7.58172 14 4 17.5817 4 22" stroke="currentColor" strokeWidth="2"/>
                                </svg>
                            </button>
                            <CartIndicator navigate={this.props.navigate} />
                        </div>
                    </nav>
                </header>

                <main className="main-content">
                    <div className="container admin-container">
                        <div className="admin-header">
                            <h1 className="admin-title">Админ панель</h1>
                            <button className="back-button" onClick={this.handleGoBack}>
                                Назад
                            </button>
                        </div>

                        <div className="admin-tabs">
                            <button 
                                className={`tab ${activeTab === 'dashboard' ? 'active' : ''}`}
                                onClick={() => this.switchTab('dashboard')}
                            >
                                Статистика
                            </button>
                            <button 
                                className={`tab ${activeTab === 'users' ? 'active' : ''}`}
                                onClick={() => this.switchTab('users')}
                            >
                                Пользователи
                            </button>
                            <button 
                                className={`tab ${activeTab === 'orders' ? 'active' : ''}`}
                                onClick={() => this.switchTab('orders')}
                            >
                                Заказы
                            </button>
                        </div>

                        {loading && (
                            <div className="loading-spinner">
                                <div className="spinner"></div>
                                <p>Загрузка...</p>
                            </div>
                        )}

                        {error && (
                            <div className="error-message">
                                <p>{error}</p>
                            </div>
                        )}

                        {!loading && !error && (
                            <div className="admin-content">
                                {activeTab === 'dashboard' && this.renderDashboard()}
                                {activeTab === 'users' && this.renderUsers()}
                                {activeTab === 'orders' && this.renderOrders()}
                            </div>
                        )}
                    </div>
                </main>
            </div>
        );
    }
}

export default withNavigate(AdminPanel);