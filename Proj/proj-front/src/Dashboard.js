import { useContext, useEffect, useState } from 'react';
import { AuthContext } from './context/AuthContext';
import API from './api';
import TaskList from './TaskList';
import UserList from './UserList';
import AddUserForm from './AddUserForm';
import Sidebar from './Sidebar';
import CreateTask from './CreateTask';
import { FaTasks, FaUserFriends, FaChartLine } from 'react-icons/fa';

const Dashboard = () => {
  const { auth } = useContext(AuthContext);
  const [tasks, setTasks] = useState([]);
  const [allTasks, setAllTasks] = useState([]);
  const [users, setUsers] = useState([]);
  const [currentSection, setCurrentSection] = useState('dashboard');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      if (!auth) return;
      setLoading(true);
      try {
        if (auth.role === 'admin' || auth.role === 'manager') {
          const userResponse = await API.get('/api/User/all', {
            headers: { Authorization: `Bearer ${auth.accessToken}` },
          });
          setUsers(userResponse.data);
        }

        if (auth.role === 'admin' || auth.role === 'manager') {
          const allTasksResponse = await API.get('/api/Tasks', {
            headers: { Authorization: `Bearer ${auth.accessToken}` },
          });
          setAllTasks(allTasksResponse.data);
        }

        if (auth.role === 'manager' || auth.role === 'worker') {
          const taskResponse = await API.get(`/api/Tasks/user/${auth.id}`, {
            headers: { Authorization: `Bearer ${auth.accessToken}` },
          });
          setTasks(taskResponse.data);
        }
      } catch (error) {
        console.error('Failed to fetch data:', error);
      } finally {
        setLoading(false);
      }
    };
    if (auth) fetchData();
  }, [auth]);

  const handleDelete = async (userId) => {
    try {
      await API.delete(`/api/User/${userId}`, {
        headers: { Authorization: `Bearer ${auth.accessToken}` },
      });
      setUsers((prevUsers) => prevUsers.filter(user => user.id !== userId));
    } catch (error) {
      console.error('Failed to delete user:', error);
    }
  };

  const updateTask = async (task) => {
    const response = await API.fetch(`/api/Tasks/${task.id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status: task.status, progress: task.progress }),
    });
    if (!response.ok) {
      throw new Error('Failed to update task');
    }
  };

  const handleAddUser = async (newUser) => {
    try {
      const response = await API.post('/api/Auth/register', newUser, {
        headers: { Authorization: `Bearer ${auth.accessToken}` },
      });
      setUsers((prevUsers) => [...prevUsers, response.data]);
    } catch (error) {
      console.error('Failed to add user:', error);
    }
  };

  const renderSection = () => {
    switch (currentSection) {
      case 'users':
        return <UserList users={users} onDelete={handleDelete} onAdd={handleAddUser} currentUser={auth} />;
      case 'addUser':
        return <AddUserForm onAdd={handleAddUser} />;
      case 'tasks':
        return <TaskList initialTasks={allTasks} users={users} />;
      case 'myTasks':
        return <TaskList initialTasks={tasks} users={users} />;
      case 'createTask':
        return <CreateTask />;
      case 'dashboard':
      default:
        return (
          <div className="flex flex-col items-center justify-center text-center p-10 animate-fadeIn">
            <h1 className="text-4xl font-bold text-blue-600 mb-6 animate-bounce">Welcome to Your Dashboard</h1>
            <p className="text-lg text-gray-600 mb-8">Get an overview of your tasks and team members here!</p>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6 w-full max-w-4xl">
              <div
                className="bg-white p-6 rounded-lg shadow-lg transform transition duration-300 hover:scale-105 cursor-pointer"
                onClick={() => setCurrentSection('tasks')}
              >
                <FaTasks className="text-blue-500 text-4xl mb-4" />
                <h2 className="text-2xl font-semibold mb-2">Manage Tasks</h2>
                <p className="text-gray-600">View and manage all your tasks efficiently.</p>
              </div>
              <div
                className="bg-white p-6 rounded-lg shadow-lg transform transition duration-300 hover:scale-105 cursor-pointer"
                onClick={() => setCurrentSection('users')}
              >
                <FaUserFriends className="text-green-500 text-4xl mb-4" />
                <h2 className="text-2xl font-semibold mb-2">Your Team</h2>
                <p className="text-gray-600">Monitor the progress and performance of your team.</p>
              </div>
              <div
                className="bg-white p-6 rounded-lg shadow-lg transform transition duration-300 hover:scale-105 cursor-pointer"
                onClick={() => setCurrentSection('myTasks')}
              >
                <FaChartLine className="text-purple-500 text-4xl mb-4" />
                <h2 className="text-2xl font-semibold mb-2">Track Progress</h2>
                <p className="text-gray-600">Check overall progress with detailed analytics.</p>
              </div>
            </div>
          </div>
        );
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-16 w-16 border-t-4 border-blue-500"></div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex">
      <Sidebar setCurrentSection={setCurrentSection} />
      <div className="flex-grow bg-gray-100 p-8 ml-16">  
        <div className="bg-white shadow-lg rounded-lg p-6">
          {renderSection()}
        </div>
      </div>
    </div>
  );
};

export default Dashboard;
