import axios from 'axios';

const BASE_URL = 'http://localhost:5214/api';

class AxiosServices {
    constructor() {
        this.baseURL = BASE_URL;

        this.client = axios.create({
            baseURL: this.baseURL,
            headers: {
                'Content-Type': 'application/json'
            },
            timeout: 30000
        });

        this.client.interceptors.request.use(
            (config) => {
                const token = localStorage.getItem('token');
                if (token) {
                    config.headers.Authorization = `Bearer ${token}`;
                    console.log('JWT токен добавлен в запрос');
                }
                console.log(`${config.method?.toUpperCase()} ${config.url}`);
                return config;
            },
            (error) => {
                console.error('Request error:', error);
                return Promise.reject(error);
            }
        );

        this.client.interceptors.response.use(
            (response) => {
                console.log(`${response.status} ${response.config.url}`);
                return response;
            },
            (error) => {
                if (error.response?.status === 401) {
                    console.error('Unauthorized - возможно, истек токен');
                }
                return Promise.reject(error);
            }
        );
    }

    post(url, data, isRequired = false, headers = null) {
        const fullUrl = `${this.baseURL}${url}`;
        const config = isRequired && headers ? { headers } : {};
        return this.client.post(url, data, config);
    }

    get(url, isRequired = false, headers = null) {
        const fullUrl = `${this.baseURL}${url}`;
        const config = isRequired && headers ? { headers } : {};
        return this.client.get(url, config);
    }

    put(url, data, isRequired = false, headers = null) {
        return this.client.put(url, data, isRequired && headers ? { headers } : {});
    }

    delete(url, isRequired = false, headers = null) {
        return this.client.delete(url, isRequired && headers ? { headers } : {});
    }
}

export default AxiosServices;