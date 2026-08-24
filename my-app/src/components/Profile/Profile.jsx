import React, { useState, useEffect, useCallback } from 'react';
import axios from 'axios';
import './Profile.css';
import { useNavigate } from 'react-router-dom';

const Profile = () => {
    const navigate = useNavigate();
    const [userData, setUserData] = useState(null);
    const [userStats, setUserStats] = useState(null);
    const [userPurchases, setUserPurchases] = useState([]);
    const [editMode, setEditMode] = useState(false);
    const [formData, setFormData] = useState({});
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [loading, setLoading] = useState(true);

    const userId = localStorage.getItem('userId');
    const token = localStorage.getItem('token');

    useEffect(() => {
        if (!userId && !token) {
            navigate('/SignIn');
            return;
        }
        
        const needRefresh = localStorage.getItem('needRefreshProfile');
        console.log('Проверка флага needRefreshProfile:', needRefresh);
        
        if (needRefresh === 'true') {
            console.log('Нужно обновить данные после заказа');
            localStorage.removeItem('needRefreshProfile');
            const cacheKeys = ['profile_user_data', 'profile_user_stats', 'profile_user_purchases'];
            cacheKeys.forEach(key => {
                localStorage.removeItem(key);
                localStorage.removeItem(`${key}_time`);
            });
            console.log('Кэш очищен');
        }
        
        refreshAllData();
        
        const handleVisibilityChange = () => {
            if (!document.hidden) {
                console.log('Страница активна, обновляем данные');
                refreshAllData();
            }
        };
        
        const handleFocus = () => {
            console.log('Окно в фокусе, обновляем данные');
            refreshAllData();
        };
        
        document.addEventListener('visibilitychange', handleVisibilityChange);
        window.addEventListener('focus', handleFocus);
        
        return () => {
            document.removeEventListener('visibilitychange', handleVisibilityChange);
            window.removeEventListener('focus', handleFocus);
        };
    }, [navigate, userId, token]);

    const fetchUserData = useCallback(async () => {
        try {
            const token = localStorage.getItem('token');
            const userId = localStorage.getItem('userId');
            
            if (!userId) {
                throw new Error('User ID not found');
            }

            console.log('Загрузка данных пользователя с ID:', userId);
            
            const response = await axios.get(`http://localhost:5214/api/Users/${userId}`, {
                headers: { 
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json',
                    'Cache-Control': 'no-cache',
                    'Pragma': 'no-cache'
                }
            });
            
            console.log('Полные данные пользователя от сервера:', response.data);

            const userName = response.data.userName || response.data.UserName || 'Пользователь';
            const userRole = response.data.role || response.data.Role || 'User';
            const createdAt = response.data.createdAt || response.data.CreatedAt;
            
            setUserData(response.data);
            setFormData({
                userName: userName,
                role: userRole
            });

            if (userName !== localStorage.getItem('userName')) {
                localStorage.setItem('userName', userName);
            }
            
        } catch (error) {
            console.error('Ошибка загрузки данных пользователя:', error);
            if (error.response?.status === 401) {
                localStorage.removeItem('token');
                localStorage.removeItem('userId');
                navigate('/SignIn');
            }
            setError('Не удалось загрузить данные профиля');
        }
    }, [navigate]);

    const fetchUserStats = useCallback(async () => {
        try {
            const token = localStorage.getItem('token');
            const userId = localStorage.getItem('userId');
            
            if (!userId) return;

            console.log('Загрузка статистики для пользователя ID:', userId);

            let basketCount = 0;
            try {
                const basketResponse = await axios.get(`http://localhost:5214/api/Baskets/user/${userId}`, {
                    headers: { 
                        'Authorization': `Bearer ${token}`,
                        'Content-Type': 'application/json'
                    }
                });
                if (basketResponse.data && Array.isArray(basketResponse.data)) {
                    basketCount = basketResponse.data.reduce((sum, item) => sum + (item.Quantity || 0), 0);
                }
                console.log('Товаров в корзине:', basketCount);
            } catch (error) {
                console.error('Ошибка загрузки корзины:', error);
            }

            let purchases = [];
            let totalSpent = 0;
            let purchaseCount = 0;
            
            try {
                const purchasesResponse = await axios.get(`http://localhost:5214/api/Users/${userId}/purchases`, {
                    headers: { 
                        'Authorization': `Bearer ${token}`,
                        'Content-Type': 'application/json'
                    }
                });
                purchases = purchasesResponse.data || [];
                purchaseCount = purchases.length;
                totalSpent = purchases.reduce((sum, order) => sum + (order.totalAmount || order.TotalAmount || 0), 0);
                console.log('Количество заказов:', purchaseCount);
                console.log('Потрачено:', totalSpent);
            } catch (error) {
                console.error('Ошибка загрузки покупок для статистики:', error);
            }

            let reviewCount = 0;
            try {
                const reviewsResponse = await axios.get(`http://localhost:5214/api/Reviews/user/${userId}/count`, {
                    headers: { 
                        'Authorization': `Bearer ${token}`,
                        'Content-Type': 'application/json'
                    }
                });
                reviewCount = reviewsResponse.data?.count || 0;
                console.log('Количество отзывов:', reviewCount);
            } catch (error) {
                console.error('Ошибка загрузки количества отзывов:', error);
                reviewCount = 0;
            }

            const stats = {
                basketCount: basketCount,
                purchaseCount: purchaseCount,
                reviewCount: reviewCount,
                totalSpent: totalSpent
            };

            setUserStats(stats);
            
        } catch (error) {
            console.error('Ошибка загрузки статистики:', error);
            setUserStats({
                basketCount: 0,
                purchaseCount: 0,
                reviewCount: 0,
                totalSpent: 0
            });
        }
    }, []);

    const fetchUserPurchases = useCallback(async () => {
        try {
            const token = localStorage.getItem('token');
            const userId = localStorage.getItem('userId');
            
            if (!userId) {
                console.log('Нет userId в localStorage');
                return;
            }

            console.log('Запрос заказов для пользователя ID:', userId);
            
            const response = await axios.get(`http://localhost:5214/api/Users/${userId}/purchases`, {
                headers: { 
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                }
            });
            
            console.log('Ответ сервера (заказы):', response.data);
            
            let purchases = response.data || [];

            const normalizedPurchases = purchases.map(order => ({
                id: order.id || order.Id || order.orderId || order.OrderId,
                orderNumber: order.orderNumber || order.OrderNumber || order.number || order.Number || order.id || order.Id,
                totalAmount: order.totalAmount || order.TotalAmount || order.total || order.Total || order.amount || order.Amount || 0,
                status: order.status || order.Status || 'pending',
                createdAt: order.createdAt || order.CreatedAt || order.date || order.Date || order.orderDate || order.OrderDate,
                items: order.items || order.Items || order.products || order.Products || []
            }));
            
            console.log('Нормализованные заказы:', normalizedPurchases);
            
            setUserPurchases(normalizedPurchases);
            
        } catch (error) {
            console.error('Ошибка загрузки покупок:', error);
            setUserPurchases([]);
        }
    }, []);

    const refreshAllData = useCallback(async () => {
        console.log('Начало полного обновления данных');
        setLoading(true);
        await Promise.all([
            fetchUserData(),
            fetchUserStats(),
            fetchUserPurchases()
        ]);
        setLoading(false);
        console.log('Полное обновление данных завершено');
    }, [fetchUserData, fetchUserStats, fetchUserPurchases]);

    const handleSave = async () => {
        try {
            setError('');
            setSuccess('');
            
            const token = localStorage.getItem('token');
            const userId = localStorage.getItem('userId');
            
            if (!userId || !token) {
                setError('Сессия истекла. Пожалуйста, войдите заново.');
                navigate('/SignIn');
                return;
            }
            
            console.log('Отправка данных для обновления профиля:');
            console.log('userId:', userId);
            console.log('Новое имя:', formData.userName);

            const response = await axios.get(`http://localhost:5214/api/Users/force-update-name/${userId}/${encodeURIComponent(formData.userName)}`, {
                headers: { 
                    'Authorization': `Bearer ${token}`
                }
            });
            
            console.log('Ответ сервера:', response.data);
            
            if (response.data.success) {
                const newName = response.data.currentNameInDb || formData.userName;

                localStorage.setItem('userName', newName);

                setUserData(prev => ({ ...prev, userName: newName, UserName: newName }));
                setFormData(prev => ({ ...prev, userName: newName }));
                
                setSuccess(`Имя успешно изменено на "${newName}"`);
                setEditMode(false);
                
                setTimeout(() => setSuccess(''), 3000);
            } else {
                setError('Не удалось обновить имя. Попробуйте позже.');
            }
            
        } catch (error) {
            console.error('Ошибка сохранения:', error);
            setError('Ошибка при сохранении профиля');
        }
    };

    const handleChange = (e) => {
        setFormData({
            ...formData,
            [e.target.name]: e.target.value
        });
    };

    const handleGoBack = () => {
        navigate(-1);
    };

    const handleAdmin = () => {
        navigate('/Admin')
    };

    const handleLogout = () => {
        localStorage.removeItem('userId');
        localStorage.removeItem('userName');
        localStorage.removeItem('userRole');
        localStorage.removeItem('token');
        navigate('/SignIn');
    };

    const handlePasswordChange = async () => {
        const oldPassword = prompt('Введите текущий пароль:');
        const newPassword = prompt('Введите новый пароль:');
        const confirmPassword = prompt('Подтвердите новый пароль:');

        if (!oldPassword || !newPassword || !confirmPassword) {
            setError('Все поля должны быть заполнены');
            return;
        }

        if (newPassword !== confirmPassword) {
            setError('Пароли не совпадают');
            return;
        }

        if (newPassword.length < 6) {
            setError('Пароль должен содержать минимум 6 символов');
            return;
        }

        try {
            const token = localStorage.getItem('token');
            const userId = localStorage.getItem('userId');
            
            const data = {
                oldPassword: oldPassword,
                newPassword: newPassword
            };
            
            console.log('Отправляем данные для смены пароля:', data);
            console.log('URL:', `http://localhost:5214/api/Users/${userId}/password`);
            console.log('Headers:', { Authorization: `Bearer ${token}` });
            
            const response = await axios.patch(
                `http://localhost:5214/api/Users/${userId}/password`,
                data,
                {
                    headers: { 
                        'Authorization': `Bearer ${token}`,
                        'Content-Type': 'application/json'
                    }
                }
            );
            
            console.log('Ответ сервера:', response.data);
            
            setSuccess('Пароль успешно изменен. Вы будете перенаправлены на страницу входа.');

            localStorage.removeItem('token');
            localStorage.removeItem('userId');
            localStorage.removeItem('userName');
            localStorage.removeItem('userRole');

            setTimeout(() => {
                navigate('/SignIn');
            }, 2000);
            
        } catch (error) {
            console.error('Ошибка при смене пароля:', error);
            console.error('Статус:', error.response?.status);
            console.error('Данные ошибки:', error.response?.data);
            
            if (error.response?.status === 400) {
                setError(error.response.data?.message || 'Неверный текущий пароль');
            } else if (error.response?.status === 401) {
                setError('Сессия истекла. Пожалуйста, войдите заново.');
                localStorage.clear();
                setTimeout(() => navigate('/SignIn'), 2000);
            } else {
                setError('Ошибка при смене пароля. Попробуйте позже.');
            }
        }
    };

    const formatDate = (dateString) => {
        if (!dateString) return 'Дата не указана';
        try {
            const date = new Date(dateString);
            if (isNaN(date.getTime())) return 'Дата не указана';
            return date.toLocaleDateString('ru-RU', {
                day: '2-digit',
                month: '2-digit',
                year: 'numeric'
            });
        } catch {
            return 'Дата не указана';
        }
    };

    const getStatusBadge = (status) => {
        const statusMap = {
            'pending': { text: 'В обработке', className: 'status-pending' },
            'processing': { text: 'Обрабатывается', className: 'status-processing' },
            'paid': { text: 'Оплачен', className: 'status-paid' },
            'shipped': { text: 'Отправлен', className: 'status-shipped' },
            'delivered': { text: 'Доставлен', className: 'status-delivered' },
            'completed': { text: 'Завершен', className: 'status-completed' },
            'cancelled': { text: 'Отменен', className: 'status-cancelled' },
            'canceled': { text: 'Отменен', className: 'status-cancelled' }
        };
        
        const statusKey = String(status || 'pending').toLowerCase();
        return statusMap[statusKey] || { text: status || 'Неизвестно', className: 'status-unknown' };
    };

    const displayUserName = userData?.userName || userData?.UserName || localStorage.getItem('userName') || formData.userName || 'Пользователь';
    const displayRole = userData?.role || userData?.Role || formData.role || 'Пользователь';
    const displayCreatedAt = userData?.createdAt || userData?.CreatedAt || 'Неизвестно';

    console.log('Отображаемое имя:', displayUserName);
    console.log('userData.userName:', userData?.userName);
    console.log('localStorage.userName:', localStorage.getItem('userName'));
    console.log('formData.userName:', formData.userName);

    if (loading) {
        return (
            <div className="loading-spinner">
                <div className="spinner"></div>
                <p>Загрузка профиля...</p>
            </div>
        );
    }

    return (
        <div className="profile-page">
            <div className="container">
                <div className="profile-header">
                    <h1 className="profile-title">Мой профиль</h1>
                    <div className="profile-actions">
                        <button 
                            className="btn-edit"
                            onClick={() => setEditMode(!editMode)}
                        >
                            {editMode ? 'Отмена' : 'Редактировать'}
                        </button>
                        <button 
                            className="btn-password"
                            onClick={handlePasswordChange}
                        >
                            Сменить пароль
                        </button>
                        <button 
                            className="btn-logout"
                            onClick={handleLogout}
                        >
                            Выйти
                        </button>
                    </div>
                </div>

                <button className="back-button" onClick={handleGoBack}>
                    Назад
                </button>

                {localStorage.getItem('userRole') === 'Admin' && (
                    <button className="back-button" onClick={handleAdmin}>
                        Админ панель
                    </button>
                )}

                {error && (
                    <div className="alert alert-error">
                        <p>{error}</p>
                        <button onClick={() => setError('')}>×</button>
                    </div>
                )}
                {success && (
                    <div className="alert alert-success">
                        <p>{success}</p>
                        <button onClick={() => setSuccess('')}>×</button>
                    </div>
                )}

                <div className="profile-content">
                    <div className="profile-info-column">
                        <div className="profile-card">
                            <div className="user-header">
                                <div className="user-initials">
                                    {displayUserName ? displayUserName[0].toUpperCase() : 'U'}
                                </div>
                                <div className="user-basic-info">
                                    <h2>{displayUserName}</h2>
                                    <p className="username">@{displayUserName}</p>
                                    <p className="user-role">{displayRole}</p>
                                    <p className="member-since">
                                        Зарегистрирован: {formatDate(displayCreatedAt)}
                                    </p>
                                </div>
                            </div>

                            <div className="profile-form">
                                <div className="form-group">
                                    <label>Имя пользователя</label>
                                    {editMode ? (
                                        <input 
                                            type="text"
                                            name="userName"
                                            value={formData.userName || ''}
                                            onChange={handleChange}
                                            className="form-input"
                                            placeholder="Введите имя пользователя"
                                        />
                                    ) : (
                                        <p className="form-value">{displayUserName}</p>
                                    )}
                                </div>

                                <div className="form-group">
                                    <label>Роль</label>
                                    <p className="form-value">{displayRole}</p>
                                </div>

                                <div className="form-group">
                                    <label>Дата регистрации</label>
                                    <p className="form-value">{formatDate(displayCreatedAt)}</p>
                                </div>

                                <div className="form-group">
                                    <label>ID пользователя</label>
                                    <p className="form-value">{userId}</p>
                                </div>

                                {editMode && (
                                    <div className="form-actions">
                                        <button 
                                            className="btn-save"
                                            onClick={handleSave}
                                        >
                                            Сохранить изменения
                                        </button>
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>

                    <div className="profile-stats-column">
                        <div className="stats-card">
                            <h3 className="stats-title">Статистика</h3>
                            <div className="stats-grid">
                                <div className="stat-item">
                                    <div className="stat-value">{userStats?.basketCount || 0}</div>
                                    <div className="stat-label">Товаров в корзине</div>
                                </div>
                                <div className="stat-item">
                                    <div className="stat-value">{userStats?.purchaseCount || 0}</div>
                                    <div className="stat-label">Заказов</div>
                                </div>
                                <div className="stat-item">
                                    <div className="stat-value">{userStats?.reviewCount || 0}</div>
                                    <div className="stat-label">Отзывов</div>
                                </div>
                                <div className="stat-item">
                                    <div className="stat-value">
                                        {userStats?.totalSpent ? `${userStats.totalSpent.toLocaleString('ru-RU')} ₽` : '0 ₽'}
                                    </div>
                                    <div className="stat-label">Потрачено</div>
                                </div>
                            </div>
                        </div>

                        <div className="basket-card">
                            <h3 className="basket-title">Корзина</h3>
                            <div className="basket-stats">
                                <p className="basket-count">
                                    Товаров в корзине: <strong>{userStats?.basketCount || 0}</strong>
                                </p>
                            </div>
                            <button 
                                className="btn-view-basket"
                                onClick={() => window.location.href = '/cart'}
                            >
                                Перейти в корзину
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Profile;